using FluentAssertions;
using StorageWatch.Config;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting.Plugins;
using StorageWatch.Services.AutoUpdate;
using StorageWatch.Services.Logging;
using StorageWatch.Shared.Update.Models;
using StorageWatch.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StorageWatch.Tests.UnitTests
{
    public class PluginUpdatePipelineTests
    {
        [Fact]
        public async Task PluginUpdateChecker_UsesVersionComparison_AndIncludesOnlyEligibleUpdates()
        {
            var manifestUrl = "https://updates.test/manifest.json";
            var manifestJson = "{" +
                "\"version\":\"1.0.0\"," +
                "\"plugins\":[" +
                    "{\"id\":\"TestPlugin\",\"version\":\"0.9.0\",\"downloadUrl\":\"https://updates.test/testplugin-090.zip\",\"sha256\":\"abc\"}," +
                    "{\"id\":\"TestPlugin\",\"version\":\"1.1.0\",\"downloadUrl\":\"https://updates.test/testplugin-110.zip\",\"sha256\":\"def\"}," +
                    "{\"id\":\"NewPlugin\",\"version\":\"1.0.0\",\"downloadUrl\":\"https://updates.test/newplugin.zip\",\"sha256\":\"ghi\"}" +
                "]" +
            "}";

            using var httpClient = new HttpClient(new StaticResponseHttpMessageHandler(manifestUrl, manifestJson));
            var options = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                ManifestUrl = manifestUrl,
                CheckIntervalMinutes = 60
            });
            var checker = new PluginUpdateChecker(httpClient, new TestLogger<PluginUpdateChecker>(), options);

            var previousRegistry = AlertSenderPluginRegistry.Current;
            try
            {
                var registry = new AlertSenderPluginRegistry();
                registry.RegisterPlugin<TestAlertSender>("TestPlugin"); // local version defaults to 1.0.0

                var result = await checker.CheckForUpdatesAsync(CancellationToken.None);

                result.ErrorMessage.Should().BeNullOrEmpty();
                result.Updates.Should().Contain(u => u.Id == "TestPlugin" && u.Version == "1.1.0");
                result.Updates.Should().Contain(u => u.Id == "NewPlugin" && u.Version == "1.0.0");
                result.Updates.Should().NotContain(u => u.Id == "TestPlugin" && u.Version == "0.9.0");
            }
            finally
            {
                RestorePluginRegistry(previousRegistry);
            }
        }

        [Fact]
        public async Task PluginUpdateDownloader_DownloadsZip_AndVerifiesHash()
        {
            var sourceDirectory = TestHelpers.CreateTempDirectory();
            var zipPath = Path.Combine(TestHelpers.CreateTempDirectory(), "plugin-update.zip");
            var pluginFile = Path.Combine(sourceDirectory, "plugins", "TestPlugin", "TestPlugin.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(pluginFile)!);
            await File.WriteAllTextAsync(pluginFile, "plugin-binary-v1", Encoding.UTF8);
            ZipFile.CreateFromDirectory(sourceDirectory, zipPath);

            var zipBytes = await File.ReadAllBytesAsync(zipPath);
            var hash = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant();

            var plugin = new PluginUpdateInfo
            {
                Id = "TestPlugin",
                Version = "1.1.0",
                DownloadUrl = "https://updates.test/plugin-update.zip",
                Sha256 = hash
            };

            using var httpClient = new HttpClient(new BinaryResponseHttpMessageHandler(plugin.DownloadUrl, zipBytes));
            var downloader = new PluginUpdateDownloader(httpClient, new TestLogger<PluginUpdateDownloader>());

            var result = await downloader.DownloadAsync(plugin, CancellationToken.None);

            result.Success.Should().BeTrue();
            result.FilePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.FilePath!).Should().BeTrue();

            var downloadedBytes = await File.ReadAllBytesAsync(result.FilePath!);
            downloadedBytes.Should().Equal(zipBytes);

            File.Delete(result.FilePath!);
        }

        [Fact]
        public async Task PluginUpdateInstaller_ExtractsAndInstallsPayload_AndCleansStagingDirectory()
        {
            var sourceDirectory = TestHelpers.CreateTempDirectory();
            var pluginDirectory = TestHelpers.CreateTempDirectory();
            var zipPath = Path.Combine(TestHelpers.CreateTempDirectory(), "plugin-update.zip");

            var pluginFile = Path.Combine(sourceDirectory, "plugins", "TestPlugin", "TestPlugin.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(pluginFile)!);
            await File.WriteAllTextAsync(pluginFile, "plugin-binary-v2", Encoding.UTF8);
            ZipFile.CreateFromDirectory(sourceDirectory, zipPath);

            var beforeStaging = SnapshotTempChildDirectories("StorageWatchPluginUpdate");

            var installer = new PluginUpdateInstaller(new TestLogger<PluginUpdateInstaller>(), pluginDirectory);
            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            result.Success.Should().BeTrue();

            var installedPluginFile = Path.Combine(pluginDirectory, "plugins", "TestPlugin", "TestPlugin.dll");
            File.Exists(installedPluginFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(installedPluginFile, Encoding.UTF8);
            content.Should().Be("plugin-binary-v2");

            var afterStaging = SnapshotTempChildDirectories("StorageWatchPluginUpdate");
            var retries = 20;
            while (retries-- > 0 && afterStaging.Except(beforeStaging).Any())
            {
                await Task.Delay(50);
                afterStaging = SnapshotTempChildDirectories("StorageWatchPluginUpdate");
            }

            afterStaging.Except(beforeStaging).Should().BeEmpty();
        }

        [Fact]
        public async Task AutoUpdateWorker_PluginPipeline_RunsInCheckDownloadInstallSequence()
        {
            var sourceDirectory = TestHelpers.CreateTempDirectory();
            var pluginInstallDirectory = TestHelpers.CreateTempDirectory();
            var zipPath = Path.Combine(TestHelpers.CreateTempDirectory(), "plugin-update.zip");

            var pluginFile = Path.Combine(sourceDirectory, "plugins", "TestPlugin", "TestPlugin.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(pluginFile)!);
            await File.WriteAllTextAsync(pluginFile, "plugin-binary-v3", Encoding.UTF8);
            ZipFile.CreateFromDirectory(sourceDirectory, zipPath);
            var zipBytes = await File.ReadAllBytesAsync(zipPath);
            var hash = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant();

            var manifestUrl = "https://updates.test/manifest.json";
            var packageUrl = "https://updates.test/plugin-update.zip";
            var manifestJson = "{" +
                "\"version\":\"1.0.0\"," +
                "\"plugins\":[" +
                    $"{{\"id\":\"TestPlugin\",\"version\":\"1.1.0\",\"downloadUrl\":\"{packageUrl}\",\"sha256\":\"{hash}\"}}" +
                "]" +
            "}";

            var handler = new MultiResponseHttpMessageHandler(new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase)
            {
                [manifestUrl] = () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifestJson, Encoding.UTF8, "application/json")
                },
                [packageUrl] = () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipBytes)
                }
            });

            using var httpClient = new HttpClient(handler);
            var options = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                ManifestUrl = manifestUrl,
                CheckIntervalMinutes = 60
            });

            var previousRegistry = AlertSenderPluginRegistry.Current;
            var sequence = new List<string>();
            try
            {
                var registry = new AlertSenderPluginRegistry();
                registry.RegisterPlugin<TestAlertSender>("TestPlugin");

                var realChecker = new PluginUpdateChecker(httpClient, new TestLogger<PluginUpdateChecker>(), options);
                var realDownloader = new PluginUpdateDownloader(httpClient, new TestLogger<PluginUpdateDownloader>());
                var realInstaller = new PluginUpdateInstaller(new TestLogger<PluginUpdateInstaller>(), pluginInstallDirectory);

                var worker = new AutoUpdateWorker(
                    new TestOptionsMonitor<StorageWatchOptions>(new StorageWatchOptions { Mode = StorageWatchMode.Agent }),
                    options,
                    new NoOpServiceUpdateChecker(),
                    new NoOpServiceUpdateDownloader(),
                    new NoOpServiceUpdateInstaller(),
                    new TrackingPluginUpdateChecker(realChecker, sequence),
                    new TrackingPluginUpdateDownloader(realDownloader, sequence),
                    new TrackingPluginUpdateInstaller(realInstaller, sequence),
                    new SingleTickTimerFactory(),
                    new RollingFileLogger(TestHelpers.CreateTempLogFile()));

                await worker.RunUpdateCycleAsync(CancellationToken.None);

                sequence.Should().ContainInOrder(
                    "plugin-check",
                    "plugin-download:TestPlugin",
                    "plugin-install");

                var installedPluginFile = Path.Combine(pluginInstallDirectory, "plugins", "TestPlugin", "TestPlugin.dll");
                File.Exists(installedPluginFile).Should().BeTrue();
                var installedContent = await File.ReadAllTextAsync(installedPluginFile, Encoding.UTF8);
                installedContent.Should().Be("plugin-binary-v3");
            }
            finally
            {
                RestorePluginRegistry(previousRegistry);
            }
        }

        private static HashSet<string> SnapshotTempChildDirectories(string folderName)
        {
            var root = Path.Combine(Path.GetTempPath(), folderName);
            return new HashSet<string>(
                Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void RestorePluginRegistry(AlertSenderPluginRegistry? previous)
        {
            var field = typeof(AlertSenderPluginRegistry).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, previous);
        }

        private sealed class StaticResponseHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _url;
            private readonly string _responseBody;

            public StaticResponseHttpMessageHandler(string url, string responseBody)
            {
                _url = url;
                _responseBody = responseBody;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var requestUrl = request.RequestUri?.ToString() ?? string.Empty;
                if (!string.Equals(requestUrl, _url, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class BinaryResponseHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _url;
            private readonly byte[] _payload;

            public BinaryResponseHttpMessageHandler(string url, byte[] payload)
            {
                _url = url;
                _payload = payload;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var requestUrl = request.RequestUri?.ToString() ?? string.Empty;
                if (!string.Equals(requestUrl, _url, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_payload)
                });
            }
        }

        private sealed class MultiResponseHttpMessageHandler : HttpMessageHandler
        {
            private readonly IReadOnlyDictionary<string, Func<HttpResponseMessage>> _responses;

            public MultiResponseHttpMessageHandler(IReadOnlyDictionary<string, Func<HttpResponseMessage>> responses)
            {
                _responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var requestUrl = request.RequestUri?.ToString() ?? string.Empty;
                if (_responses.TryGetValue(requestUrl, out var responseFactory))
                    return Task.FromResult(responseFactory());

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        private sealed class TestAlertSender : IAlertSender
        {
            public string Name => "TestAlertSender";

            public Task SendAlertAsync(DiskStatus status, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class NoOpServiceUpdateChecker : IServiceUpdateChecker
        {
            public Task<ComponentUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new ComponentUpdateCheckResult { IsUpdateAvailable = false });
            }
        }

        private sealed class NoOpServiceUpdateDownloader : IServiceUpdateDownloader
        {
            public Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken)
            {
                return Task.FromResult(new UpdateDownloadResult { Success = false, ErrorMessage = "Not used in plugin pipeline test." });
            }
        }

        private sealed class NoOpServiceUpdateInstaller : IServiceUpdateInstaller
        {
            public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
            {
                return Task.FromResult(new UpdateInstallResult { Success = false, ErrorMessage = "Not used in plugin pipeline test." });
            }
        }

        private sealed class TrackingPluginUpdateChecker : IPluginUpdateChecker
        {
            private readonly IPluginUpdateChecker _inner;
            private readonly List<string> _sequence;

            public TrackingPluginUpdateChecker(IPluginUpdateChecker inner, List<string> sequence)
            {
                _inner = inner;
                _sequence = sequence;
            }

            public async Task<PluginUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
            {
                _sequence.Add("plugin-check");
                return await _inner.CheckForUpdatesAsync(cancellationToken);
            }
        }

        private sealed class TrackingPluginUpdateDownloader : IPluginUpdateDownloader
        {
            private readonly IPluginUpdateDownloader _inner;
            private readonly List<string> _sequence;

            public TrackingPluginUpdateDownloader(IPluginUpdateDownloader inner, List<string> sequence)
            {
                _inner = inner;
                _sequence = sequence;
            }

            public async Task<PluginDownloadResult> DownloadAsync(PluginUpdateInfo plugin, CancellationToken cancellationToken)
            {
                _sequence.Add($"plugin-download:{plugin.Id}");
                return await _inner.DownloadAsync(plugin, cancellationToken);
            }
        }

        private sealed class TrackingPluginUpdateInstaller : IPluginUpdateInstaller
        {
            private readonly IPluginUpdateInstaller _inner;
            private readonly List<string> _sequence;

            public TrackingPluginUpdateInstaller(IPluginUpdateInstaller inner, List<string> sequence)
            {
                _inner = inner;
                _sequence = sequence;
            }

            public async Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
            {
                _sequence.Add("plugin-install");
                return await _inner.InstallAsync(zipPath, cancellationToken);
            }
        }

        private sealed class SingleTickTimerFactory : IAutoUpdateTimerFactory
        {
            public IAutoUpdateTimer Create(TimeSpan interval) => new SingleTickTimer();
        }

        private sealed class SingleTickTimer : IAutoUpdateTimer
        {
            public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

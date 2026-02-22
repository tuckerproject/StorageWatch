using FluentAssertions;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting.Plugins;
using StorageWatch.Services.AutoUpdate;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Tests.UnitTests
{
    public class AutoUpdateTests
    {
        [Fact]
        public void ServiceUpdateChecker_ParseManifest_ParsesExpectedFields()
        {
            var json = "{\"version\":\"1.0.0\",\"service\":{\"version\":\"1.2.3\",\"downloadUrl\":\"https://example.com/update.zip\",\"sha256\":\"abc123\"},\"plugins\":[{\"name\":\"GroupMe\",\"version\":\"2.0.0\",\"downloadUrl\":\"https://example.com/plugin.zip\",\"sha256\":\"def\"}]}";
            var manifest = ServiceUpdateChecker.ParseManifest(json);
            manifest.Should().NotBeNull();
            manifest!.Service.Should().NotBeNull();
            manifest.Service!.Version.Should().Be("1.2.3");
            manifest.Service.DownloadUrl.Should().Be("https://example.com/update.zip");
            manifest.Service.Sha256.Should().Be("abc123");
            manifest.Plugins.Should().HaveCount(1);
            manifest.Plugins[0].Name.Should().Be("GroupMe");
        }

        [Fact]
        public async Task ServiceUpdateChecker_CheckForUpdateAsync_ReturnsUpdateWhenNewerVersion()
        {
            var manifestJson = "{\"version\":\"1.0.0\",\"service\":{\"version\":\"2.0.0\",\"downloadUrl\":\"https://example.com/update.zip\",\"sha256\":\"abc\"}}";
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(manifestJson)
            });
            var httpClient = new HttpClient(handler);
            var optionsMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                ManifestUrl = "https://example.com/manifest.json",
                CheckIntervalMinutes = 60
            });
            var logger = new TestLogger<ServiceUpdateChecker>();
            var checker = new ServiceUpdateChecker(httpClient, logger, optionsMonitor);

            var result = await checker.CheckForUpdateAsync(CancellationToken.None);

            result.IsUpdateAvailable.Should().BeTrue();
            result.Component.Should().NotBeNull();
            result.Component!.Version.Should().Be("2.0.0");
        }

        [Fact]
        public void ServiceUpdateChecker_IsUpdateAvailable_ReturnsTrueForNewerVersion()
        {
            var current = new Version(1, 0, 0);
            var manifest = new Version(1, 1, 0);

            var result = ServiceUpdateChecker.IsUpdateAvailable(current, manifest);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task ServiceUpdateDownloader_DownloadsAndValidatesHash()
        {
            var content = Encoding.UTF8.GetBytes("test update payload");
            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var component = new ComponentUpdateInfo
            {
                Version = "1.0.1",
                DownloadUrl = "https://example.com/update.zip",
                Sha256 = hash
            };

            var handler = new FakeHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(content)
                };
                return response;
            });

            var httpClient = new HttpClient(handler);
            var logger = new TestLogger<ServiceUpdateDownloader>();
            var downloader = new ServiceUpdateDownloader(httpClient, logger);

            var result = await downloader.DownloadAsync(component, CancellationToken.None);

            result.Success.Should().BeTrue();
            result.FilePath.Should().NotBeNull();
            File.Exists(result.FilePath!).Should().BeTrue();

            File.Delete(result.FilePath!);
        }

        [Fact]
        public async Task ServiceUpdateDownloader_ReturnsFailureOnHashMismatch()
        {
            var content = Encoding.UTF8.GetBytes("test update payload");
            var component = new ComponentUpdateInfo
            {
                Version = "1.0.1",
                DownloadUrl = "https://example.com/update.zip",
                Sha256 = "deadbeef"
            };

            var handler = new FakeHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(content)
                };
                return response;
            });

            var httpClient = new HttpClient(handler);
            var logger = new TestLogger<ServiceUpdateDownloader>();
            var downloader = new ServiceUpdateDownloader(httpClient, logger);

            var result = await downloader.DownloadAsync(component, CancellationToken.None);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task ServiceUpdateDownloader_ReturnsFailureOnHttpError()
        {
            var component = new ComponentUpdateInfo
            {
                Version = "1.0.1",
                DownloadUrl = "https://example.com/update.zip",
                Sha256 = "abc"
            };

            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var httpClient = new HttpClient(handler);
            var logger = new TestLogger<ServiceUpdateDownloader>();
            var downloader = new ServiceUpdateDownloader(httpClient, logger);

            var result = await downloader.DownloadAsync(component, CancellationToken.None);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task ServiceUpdateInstaller_ExtractsAndCopiesFiles_TriggersRestart()
        {
            var tempSource = TestHelpers.CreateTempDirectory();
            var tempTarget = TestHelpers.CreateTempDirectory();
            var zipPath = Path.Combine(TestHelpers.CreateTempDirectory(), "update.zip");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");

            ZipFile.CreateFromDirectory(tempSource, zipPath);

            var logger = new TestLogger<ServiceUpdateInstaller>();
            var restartHandler = new FakeRestartHandler();
            var installer = new ServiceUpdateInstaller(logger, restartHandler, tempTarget);

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            var targetFile = Path.Combine(tempTarget, "app", "test.txt");
            result.Success.Should().BeTrue();
            File.Exists(targetFile).Should().BeTrue();
            var contents = await File.ReadAllTextAsync(targetFile);
            contents.Should().Be("updated");
            restartHandler.RestartRequested.Should().BeTrue();
        }

        [Fact]
        public async Task PluginUpdateChecker_ReturnsUpdatesForNewerPluginVersions()
        {
            var manifestJson = "{\"version\":\"1.0.0\",\"plugins\":[{\"name\":\"TestPlugin\",\"version\":\"2.0.0\",\"downloadUrl\":\"https://example.com/plugin.zip\",\"sha256\":\"abc\"}]}";
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(manifestJson)
            });

            var httpClient = new HttpClient(handler);
            var optionsMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                ManifestUrl = "https://example.com/manifest.json",
                CheckIntervalMinutes = 60
            });
            var logger = new TestLogger<PluginUpdateChecker>();
            var checker = new PluginUpdateChecker(httpClient, logger, optionsMonitor);

            var result = await checker.CheckForUpdatesAsync(CancellationToken.None);

            result.Updates.Should().ContainSingle();
            result.Updates[0].Name.Should().Be("TestPlugin");
        }

        [Fact]
        public async Task PluginUpdateDownloader_ReturnsFailureOnHashMismatch()
        {
            var content = Encoding.UTF8.GetBytes("plugin update payload");
            var plugin = new PluginUpdateInfo
            {
                Name = "TestPlugin",
                Version = "1.0.1",
                DownloadUrl = "https://example.com/plugin.zip",
                Sha256 = "deadbeef"
            };

            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });

            var httpClient = new HttpClient(handler);
            var logger = new TestLogger<PluginUpdateDownloader>();
            var downloader = new PluginUpdateDownloader(httpClient, logger);

            var result = await downloader.DownloadAsync(plugin, CancellationToken.None);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task PluginUpdateInstaller_ExtractsAndCopiesFiles()
        {
            var tempSource = TestHelpers.CreateTempDirectory();
            var tempTarget = TestHelpers.CreateTempDirectory();
            var zipPath = Path.Combine(TestHelpers.CreateTempDirectory(), "plugin.zip");

            var sourceFile = Path.Combine(tempSource, "plugin", "plugin.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "plugin");

            ZipFile.CreateFromDirectory(tempSource, zipPath);

            var logger = new TestLogger<PluginUpdateInstaller>();
            var installer = new PluginUpdateInstaller(logger, tempTarget);

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            var targetFile = Path.Combine(tempTarget, "plugin", "plugin.dll");
            result.Success.Should().BeTrue();
            File.Exists(targetFile).Should().BeTrue();
        }

        [Fact]
        public async Task AutoUpdateWorker_UsesTimerTicksToRunUpdateCycle()
        {
            var optionsMonitor = new TestOptionsMonitor<StorageWatchOptions>(new StorageWatchOptions { Mode = StorageWatchMode.Agent });
            var autoUpdateMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                CheckIntervalMinutes = 1
            });

            var serviceChecker = new FakeServiceUpdateChecker(new ComponentUpdateCheckResult { IsUpdateAvailable = false });
            var serviceDownloader = new FakeServiceUpdateDownloader(new UpdateDownloadResult { Success = false });
            var serviceInstaller = new FakeServiceUpdateInstaller(new UpdateInstallResult { Success = true });
            var pluginChecker = new FakePluginUpdateChecker(new PluginUpdateCheckResult { Updates = Array.Empty<PluginUpdateInfo>() });
            var pluginDownloader = new FakePluginUpdateDownloader(new PluginDownloadResult { Success = false });
            var pluginInstaller = new FakePluginUpdateInstaller(new UpdateInstallResult { Success = true });
            var timerFactory = new FakeAutoUpdateTimerFactory(new[] { true, false });
            var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());

            var worker = new TestAutoUpdateWorker(optionsMonitor, autoUpdateMonitor, serviceChecker, serviceDownloader, serviceInstaller, pluginChecker, pluginDownloader, pluginInstaller, timerFactory, logger);

            await worker.RunAsync(CancellationToken.None);

            serviceChecker.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task AutoUpdateWorker_DoesNotRunInServerMode()
        {
            var optionsMonitor = new TestOptionsMonitor<StorageWatchOptions>(new StorageWatchOptions { Mode = StorageWatchMode.Server });
            var autoUpdateMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions { Enabled = true });

            var serviceChecker = new FakeServiceUpdateChecker(new ComponentUpdateCheckResult { IsUpdateAvailable = false });
            var serviceDownloader = new FakeServiceUpdateDownloader(new UpdateDownloadResult { Success = false });
            var serviceInstaller = new FakeServiceUpdateInstaller(new UpdateInstallResult { Success = true });
            var pluginChecker = new FakePluginUpdateChecker(new PluginUpdateCheckResult { Updates = Array.Empty<PluginUpdateInfo>() });
            var pluginDownloader = new FakePluginUpdateDownloader(new PluginDownloadResult { Success = false });
            var pluginInstaller = new FakePluginUpdateInstaller(new UpdateInstallResult { Success = true });
            var timerFactory = new FakeAutoUpdateTimerFactory(new[] { true, false });
            var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());

            var worker = new TestAutoUpdateWorker(optionsMonitor, autoUpdateMonitor, serviceChecker, serviceDownloader, serviceInstaller, pluginChecker, pluginDownloader, pluginInstaller, timerFactory, logger);

            await worker.RunAsync(CancellationToken.None);

            serviceChecker.CallCount.Should().Be(0);
        }

        [Fact]
        public async Task AutoUpdateWorker_ProcessesAvailableUpdate()
        {
            var optionsMonitor = new TestOptionsMonitor<StorageWatchOptions>(new StorageWatchOptions { Mode = StorageWatchMode.Standalone });
            var autoUpdateMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions { Enabled = true });

            var component = new ComponentUpdateInfo
            {
                Version = "1.2.0",
                DownloadUrl = "https://example.com/update.zip",
                Sha256 = "hash"
            };

            var serviceChecker = new FakeServiceUpdateChecker(new ComponentUpdateCheckResult { IsUpdateAvailable = true, Component = component });
            var serviceDownloader = new FakeServiceUpdateDownloader(new UpdateDownloadResult { Success = true, FilePath = "path.zip" });
            var serviceInstaller = new FakeServiceUpdateInstaller(new UpdateInstallResult { Success = true });
            var pluginChecker = new FakePluginUpdateChecker(new PluginUpdateCheckResult { Updates = new List<PluginUpdateInfo> { new PluginUpdateInfo { Name = "TestPlugin", Version = "2.0.0", DownloadUrl = "url", Sha256 = "hash" } } });
            var pluginDownloader = new FakePluginUpdateDownloader(new PluginDownloadResult { Success = true, FilePath = "plugin.zip" });
            var pluginInstaller = new FakePluginUpdateInstaller(new UpdateInstallResult { Success = true });
            var timerFactory = new FakeAutoUpdateTimerFactory(new[] { true, false });
            var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());

            var worker = new TestAutoUpdateWorker(optionsMonitor, autoUpdateMonitor, serviceChecker, serviceDownloader, serviceInstaller, pluginChecker, pluginDownloader, pluginInstaller, timerFactory, logger);

            await worker.RunAsync(CancellationToken.None);

            serviceChecker.CallCount.Should().Be(1);
            serviceDownloader.CallCount.Should().Be(1);
            serviceInstaller.CallCount.Should().Be(1);
            pluginDownloader.CallCount.Should().Be(1);
            pluginInstaller.CallCount.Should().Be(1);
        }

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        private sealed class FakeRestartHandler : IServiceRestartHandler
        {
            public bool RestartRequested { get; private set; }

            public void RequestRestart()
            {
                RestartRequested = true;
            }
        }

        private sealed class FakeServiceUpdateChecker : IServiceUpdateChecker
        {
            private readonly ComponentUpdateCheckResult _result;

            public FakeServiceUpdateChecker(ComponentUpdateCheckResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public Task<ComponentUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakePluginUpdateChecker : IPluginUpdateChecker
        {
            private readonly PluginUpdateCheckResult _result;

            public FakePluginUpdateChecker(PluginUpdateCheckResult result)
            {
                _result = result;
            }

            public Task<PluginUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeServiceUpdateDownloader : IServiceUpdateDownloader
        {
            private readonly UpdateDownloadResult _result;

            public FakeServiceUpdateDownloader(UpdateDownloadResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakePluginUpdateDownloader : IPluginUpdateDownloader
        {
            private readonly PluginDownloadResult _result;

            public FakePluginUpdateDownloader(PluginDownloadResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public Task<PluginDownloadResult> DownloadAsync(PluginUpdateInfo plugin, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeServiceUpdateInstaller : IServiceUpdateInstaller
        {
            private readonly UpdateInstallResult _result;

            public FakeServiceUpdateInstaller(UpdateInstallResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakePluginUpdateInstaller : IPluginUpdateInstaller
        {
            private readonly UpdateInstallResult _result;

            public FakePluginUpdateInstaller(UpdateInstallResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeAutoUpdateTimerFactory : IAutoUpdateTimerFactory
        {
            private readonly bool[] _ticks;

            public FakeAutoUpdateTimerFactory(bool[] ticks)
            {
                _ticks = ticks;
            }

            public IAutoUpdateTimer Create(TimeSpan interval)
            {
                return new FakeAutoUpdateTimer(_ticks);
            }
        }

        private sealed class FakeAutoUpdateTimer : IAutoUpdateTimer
        {
            private readonly bool[] _ticks;
            private int _index;

            public FakeAutoUpdateTimer(bool[] ticks)
            {
                _ticks = ticks;
            }

            public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
            {
                var next = _index < _ticks.Length && _ticks[_index];
                _index++;
                return ValueTask.FromResult(next);
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        private sealed class TestAutoUpdateWorker : AutoUpdateWorker
        {
            public TestAutoUpdateWorker(
                Microsoft.Extensions.Options.IOptionsMonitor<StorageWatchOptions> storageOptionsMonitor,
                Microsoft.Extensions.Options.IOptionsMonitor<AutoUpdateOptions> autoUpdateOptionsMonitor,
                IServiceUpdateChecker serviceUpdateChecker,
                IServiceUpdateDownloader serviceUpdateDownloader,
                IServiceUpdateInstaller serviceUpdateInstaller,
                IPluginUpdateChecker pluginUpdateChecker,
                IPluginUpdateDownloader pluginUpdateDownloader,
                IPluginUpdateInstaller pluginUpdateInstaller,
                IAutoUpdateTimerFactory timerFactory,
                RollingFileLogger logger)
                : base(storageOptionsMonitor, autoUpdateOptionsMonitor, serviceUpdateChecker, serviceUpdateDownloader, serviceUpdateInstaller, pluginUpdateChecker, pluginUpdateDownloader, pluginUpdateInstaller, timerFactory, logger)
            {
            }

            public Task RunAsync(CancellationToken token)
            {
                return ExecuteAsync(token);
            }
        }
    }
}

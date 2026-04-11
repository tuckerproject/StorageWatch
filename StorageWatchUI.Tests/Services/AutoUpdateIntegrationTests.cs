using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StorageWatch.Shared.Update.Models;
using StorageWatchUI.Config;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.Tests.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StorageWatchUI.Tests.Services
{
    public class AutoUpdateIntegrationTests
    {
        [Fact]
        public async Task AutoUpdatePipeline_RealManifestAndZip_CompletesFullCycleAndRequestsRestart()
        {
            var testRoot = TestDirectoryFactory.CreateTempDirectory();
            var payloadRoot = Path.Combine(testRoot, "payload");
            var targetRoot = Path.Combine(testRoot, "target");
            var manifestPath = Path.Combine(testRoot, "manifest.json");
            var zipPath = Path.Combine(testRoot, "StorageWatchUI.test.zip");

            Directory.CreateDirectory(payloadRoot);
            Directory.CreateDirectory(targetRoot);

            var payloadFilePath = Path.Combine(payloadRoot, "app", "version.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(payloadFilePath)!);
            await File.WriteAllTextAsync(payloadFilePath, "integration-payload-v1", Encoding.UTF8);

            var updaterExePath = Path.Combine(targetRoot, "StorageWatch.Updater.exe");
            var systemExe = Path.Combine(Environment.SystemDirectory, "whoami.exe");
            File.Copy(systemExe, updaterExePath, overwrite: true);

            ZipFile.CreateFromDirectory(payloadRoot, zipPath);
            var zipBytes = await File.ReadAllBytesAsync(zipPath);
            var zipHash = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant();

            var currentVersion = typeof(UiUpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            var newerVersion = new Version(currentVersion.Major + 1, 0, 0, 0).ToString();

            var manifestUrl = "https://updates.test/manifest.json";
            var packageUrl = "https://updates.test/StorageWatchUI.test.zip";

            var manifestJson = $"{{\"version\":\"{newerVersion}\",\"ui\":{{\"version\":\"{newerVersion}\",\"downloadUrl\":\"{packageUrl}\",\"sha256\":\"{zipHash}\",\"releaseNotesUrl\":\"https://updates.test/release-notes\"}}}}";
            await File.WriteAllTextAsync(manifestPath, manifestJson, Encoding.UTF8);

            var handler = new FileBackedHttpMessageHandler(manifestUrl, manifestPath, packageUrl, zipPath);
            using var httpClient = new HttpClient(handler);

            var options = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                ManifestUrl = manifestUrl,
                CheckIntervalMinutes = 60
            });

            var checker = new UiUpdateChecker(httpClient, NullLogger<UiUpdateChecker>.Instance, options);
            var downloader = new UiUpdateDownloader(httpClient, NullLogger<UiUpdateDownloader>.Instance);
            var restartHandler = new FakeRestartHandler();
            var installer = new UiUpdateInstaller(
                NullLogger<UiUpdateInstaller>.Instance,
                new FakeRestartPrompter(true),
                restartHandler,
                targetRoot,
                (_, _) => true,
                () => { });

            var worker = new UiAutoUpdateWorker(
                options,
                checker,
                downloader,
                installer,
                new NoTickAutoUpdateTimerFactory(),
                NullLogger<UiAutoUpdateWorker>.Instance);

            ComponentUpdateCheckResult? checkResult = null;
            UpdateInstallResult? installResult = null;
            var restartPromptRequestedCount = 0;

            worker.UpdateCheckCompleted += (_, result) => checkResult = result;
            worker.UpdateInstallCompleted += (_, result) => installResult = result;
            worker.RestartPromptRequested += (_, _) => restartPromptRequestedCount++;

            var checkRan = await worker.TryRunUpdateCycleAsync(CancellationToken.None);
            var installRan = await worker.TryInstallAvailableUpdateAsync(CancellationToken.None);

            checkRan.Should().BeTrue();
            installRan.Should().BeTrue();
            checkResult.Should().NotBeNull();
            checkResult!.IsUpdateAvailable.Should().BeTrue();
            checkResult.Component.Should().NotBeNull();
            checkResult.Component!.Version.Should().Be(newerVersion);

            installResult.Should().NotBeNull();
            installResult!.Success.Should().BeTrue();
            restartPromptRequestedCount.Should().Be(0);
            restartHandler.RestartRequested.Should().BeFalse();

            var installedPayloadFile = Path.Combine(targetRoot, "app", "version.txt");
            File.Exists(installedPayloadFile).Should().BeFalse();
        }

        [Fact]
        public async Task AutoUpdatePipeline_RealManifestAndZip_WithHashMismatch_DoesNotInstallOrRequestRestart()
        {
            var testRoot = TestDirectoryFactory.CreateTempDirectory();
            var payloadRoot = Path.Combine(testRoot, "payload");
            var targetRoot = Path.Combine(testRoot, "target");
            var manifestPath = Path.Combine(testRoot, "manifest.json");
            var zipPath = Path.Combine(testRoot, "StorageWatchUI.test.zip");

            Directory.CreateDirectory(payloadRoot);
            Directory.CreateDirectory(targetRoot);

            var payloadFilePath = Path.Combine(payloadRoot, "app", "version.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(payloadFilePath)!);
            await File.WriteAllTextAsync(payloadFilePath, "integration-payload-v1", Encoding.UTF8);

            ZipFile.CreateFromDirectory(payloadRoot, zipPath);

            var currentVersion = typeof(UiUpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            var newerVersion = new Version(currentVersion.Major + 1, 0, 0, 0).ToString();

            var manifestUrl = "https://updates.test/manifest.json";
            var packageUrl = "https://updates.test/StorageWatchUI.test.zip";

            var manifestJson = $"{{\"version\":\"{newerVersion}\",\"ui\":{{\"version\":\"{newerVersion}\",\"downloadUrl\":\"{packageUrl}\",\"sha256\":\"deadbeef\"}}}}";
            await File.WriteAllTextAsync(manifestPath, manifestJson, Encoding.UTF8);

            var handler = new FileBackedHttpMessageHandler(manifestUrl, manifestPath, packageUrl, zipPath);
            using var httpClient = new HttpClient(handler);

            var options = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                ManifestUrl = manifestUrl,
                CheckIntervalMinutes = 60
            });

            var checker = new UiUpdateChecker(httpClient, NullLogger<UiUpdateChecker>.Instance, options);
            var downloader = new UiUpdateDownloader(httpClient, NullLogger<UiUpdateDownloader>.Instance);
            var restartHandler = new FakeRestartHandler();
            var installer = new UiUpdateInstaller(
                NullLogger<UiUpdateInstaller>.Instance,
                new FakeRestartPrompter(true),
                restartHandler,
                targetRoot);

            var worker = new UiAutoUpdateWorker(
                options,
                checker,
                downloader,
                installer,
                new NoTickAutoUpdateTimerFactory(),
                NullLogger<UiAutoUpdateWorker>.Instance);

            UpdateInstallResult? installResult = null;
            var restartPromptRequestedCount = 0;

            worker.UpdateInstallCompleted += (_, result) => installResult = result;
            worker.RestartPromptRequested += (_, _) => restartPromptRequestedCount++;

            var checkRan = await worker.TryRunUpdateCycleAsync(CancellationToken.None);
            var installRan = await worker.TryInstallAvailableUpdateAsync(CancellationToken.None);

            checkRan.Should().BeTrue();
            installRan.Should().BeFalse();
            installResult.Should().NotBeNull();
            installResult!.Success.Should().BeFalse();
            installResult.ErrorMessage.Should().NotBeNullOrWhiteSpace();
            restartPromptRequestedCount.Should().Be(0);
            restartHandler.RestartRequested.Should().BeFalse();

            var installedPayloadFile = Path.Combine(targetRoot, "app", "version.txt");
            File.Exists(installedPayloadFile).Should().BeFalse();
        }

        private sealed class FileBackedHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _manifestUrl;
            private readonly string _manifestPath;
            private readonly string _packageUrl;
            private readonly string _packagePath;

            public FileBackedHttpMessageHandler(string manifestUrl, string manifestPath, string packageUrl, string packagePath)
            {
                _manifestUrl = manifestUrl;
                _manifestPath = manifestPath;
                _packageUrl = packageUrl;
                _packagePath = packagePath;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri?.ToString();
                if (string.Equals(url, _manifestUrl, StringComparison.OrdinalIgnoreCase))
                {
                    var json = await File.ReadAllTextAsync(_manifestPath, cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                if (string.Equals(url, _packageUrl, StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = await File.ReadAllBytesAsync(_packagePath, cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(bytes)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private sealed class FakeRestartPrompter : IUiRestartPrompter
        {
            private readonly bool _response;

            public FakeRestartPrompter(bool response)
            {
                _response = response;
            }

            public bool PromptForRestart() => _response;
        }

        private sealed class FakeRestartHandler : IUiRestartHandler
        {
            public bool RestartRequested { get; private set; }

            public void RequestRestart()
            {
                RestartRequested = true;
            }
        }

        private sealed class NoTickAutoUpdateTimerFactory : IAutoUpdateTimerFactory
        {
            public IAutoUpdateTimer Create(TimeSpan interval) => new NoTickAutoUpdateTimer();
        }

        private sealed class NoTickAutoUpdateTimer : IAutoUpdateTimer
        {
            public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

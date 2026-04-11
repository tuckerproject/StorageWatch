using FluentAssertions;
using Microsoft.Extensions.Logging;
using StorageWatch.Shared.Update.Models;
using StorageWatchUI.Config;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.Tests.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StorageWatchUI.Tests.Services
{
    public class AutoUpdateTests
    {
        [Fact]
        public void UiUpdateChecker_ParseManifest_ParsesExpectedFields()
        {
            var json = "{\"version\":\"1.0.0\",\"ui\":{\"version\":\"1.2.3\",\"downloadUrl\":\"https://example.com/update.zip\",\"sha256\":\"abc123\"}}";

            var manifest = UiUpdateChecker.ParseManifest(json);

            manifest.Should().NotBeNull();
            manifest!.Ui.Should().NotBeNull();
            manifest.Ui!.Version.Should().Be("1.2.3");
            manifest.Ui.DownloadUrl.Should().Be("https://example.com/update.zip");
            manifest.Ui.Sha256.Should().Be("abc123");
        }

        [Fact]
        public async Task UiUpdateChecker_CheckForUpdateAsync_ReturnsUpdateWhenNewerVersion()
        {
            var currentVersion = typeof(UiUpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            var newerVersion = new Version(currentVersion.Major + 1, 0, 0, 0);
            var manifestJson = $"{{\"version\":\"1.0.0\",\"ui\":{{\"version\":\"{newerVersion}\",\"downloadUrl\":\"https://example.com/update.zip\",\"sha256\":\"abc\"}}}}";
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
            var logger = new TestLogger<UiUpdateChecker>();
            var checker = new UiUpdateChecker(httpClient, logger, optionsMonitor);

            var result = await checker.CheckForUpdateAsync(CancellationToken.None);

            result.IsUpdateAvailable.Should().BeTrue();
            result.Component.Should().NotBeNull();
            result.Component!.Version.Should().Be(newerVersion.ToString());
        }

        [Fact]
        public async Task UiUpdateDownloader_ReturnsFailureOnHashMismatch()
        {
            var content = Encoding.UTF8.GetBytes("test update payload");
            var component = new ComponentUpdateInfo
            {
                Version = "1.0.1",
                DownloadUrl = "https://example.com/update.zip",
                Sha256 = "deadbeef"
            };

            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });

            var httpClient = new HttpClient(handler);
            var logger = new TestLogger<UiUpdateDownloader>();
            var downloader = new UiUpdateDownloader(httpClient, logger);

            var result = await downloader.DownloadAsync(component, CancellationToken.None);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task UiUpdateInstaller_ExtractsAndCopiesFiles_RequestsRestartOnPrompt()
        {
            var tempSource = TestDirectoryFactory.CreateTempDirectory();
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var zipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "update.zip");
            var updaterExePath = Path.Combine(tempTarget, "StorageWatchUpdater.exe");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");
            ZipFile.CreateFromDirectory(tempSource, zipPath);
            await File.WriteAllTextAsync(updaterExePath, string.Empty);

            var launched = false;
            var exitRequested = false;
            var restartHandler = new FakeRestartHandler();

            var installer = new UiUpdateInstaller(
                new TestLogger<UiUpdateInstaller>(),
                new FakeRestartPrompter(true),
                restartHandler,
                tempTarget,
                (_, _) =>
                {
                    launched = true;
                    return true;
                },
                () => exitRequested = true);

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            result.Success.Should().BeTrue();
            launched.Should().BeTrue();
            exitRequested.Should().BeTrue();
            restartHandler.RestartRequested.Should().BeFalse();

            var targetFile = Path.Combine(tempTarget, "app", "test.txt");
            File.Exists(targetFile).Should().BeFalse();
        }

        [Fact]
        public async Task UiUpdateInstaller_DoesNotRequestRestart_WhenInstallFails()
        {
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var missingZipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "missing-update.zip");
            var restartHandler = new AssertiveUiRestartHandler();

            var installer = new UiUpdateInstaller(
                new TestLogger<UiUpdateInstaller>(),
                new FakeRestartPrompter(true),
                restartHandler,
                tempTarget);

            var result = await installer.InstallAsync(missingZipPath, CancellationToken.None);

            result.Success.Should().BeFalse();
            restartHandler.RequestCount.Should().Be(0);
        }

        [Fact]
        public async Task UiUpdateInstaller_StagesFilesAndLaunchesUpdater_ExitsImmediately()
        {
            var tempSource = TestDirectoryFactory.CreateTempDirectory();
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var zipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "update.zip");
            var updaterExePath = Path.Combine(tempTarget, "StorageWatchUpdater.exe");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");
            ZipFile.CreateFromDirectory(tempSource, zipPath);
            await File.WriteAllTextAsync(updaterExePath, string.Empty);

            var launched = false;
            string? launchedExe = null;
            string? launchedArgs = null;
            var exitRequested = false;

            var installer = new UiUpdateInstaller(
                new TestLogger<UiUpdateInstaller>(),
                new FakeRestartPrompter(true),
                new FakeRestartHandler(),
                tempTarget,
                (exe, args) =>
                {
                    launched = true;
                    launchedExe = exe;
                    launchedArgs = args;
                    return true;
                },
                () => exitRequested = true);

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            result.Success.Should().BeTrue();
            launched.Should().BeTrue();
            exitRequested.Should().BeTrue();
            launchedExe.Should().Be(updaterExePath);
            launchedArgs.Should().NotBeNullOrWhiteSpace();
            launchedArgs!.Should().Contain("--update-ui");
            launchedArgs.Should().Contain("--source");
            launchedArgs.Should().Contain("--target");
            launchedArgs.Should().Contain("--manifest");
            launchedArgs.Should().Contain("--restart-ui");

            var targetFile = Path.Combine(tempTarget, "app", "test.txt");
            File.Exists(targetFile).Should().BeFalse();
        }

        [Fact]
        public async Task UiUpdateInstaller_DoesNotRequestRestart_WhenUpdateIsHandedToUpdater()
        {
            var tempSource = TestDirectoryFactory.CreateTempDirectory();
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var zipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "update.zip");
            var updaterExePath = Path.Combine(tempTarget, "StorageWatchUpdater.exe");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");
            ZipFile.CreateFromDirectory(tempSource, zipPath);
            await File.WriteAllTextAsync(updaterExePath, string.Empty);

            var restartHandler = new AssertiveUiRestartHandler();

            var installer = new UiUpdateInstaller(
                new TestLogger<UiUpdateInstaller>(),
                new FakeRestartPrompter(true),
                restartHandler,
                tempTarget,
                (_, _) => true,
                () => { });

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            result.Success.Should().BeTrue();
            restartHandler.RequestCount.Should().Be(0);
        }

        [Fact]
        public async Task UiAutoUpdateWorker_UsesTimerTicksToRunUpdateCycle()
        {
            var autoUpdateMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                CheckIntervalMinutes = 1
            });

            var checker = new FakeUiUpdateChecker(new ComponentUpdateCheckResult { IsUpdateAvailable = false });
            var downloader = new FakeUiUpdateDownloader(new UpdateDownloadResult { Success = false });
            var installer = new FakeUiUpdateInstaller(new UpdateInstallResult { Success = true });
            var timerFactory = new FakeAutoUpdateTimerFactory(new[] { true, false });
            var logger = new TestLogger<UiAutoUpdateWorker>();

            var worker = new TestUiAutoUpdateWorker(autoUpdateMonitor, checker, downloader, installer, timerFactory, logger);

            await worker.RunAsync(CancellationToken.None);

            checker.CallCount.Should().Be(2);
        }

        [Fact]
        public async Task UiAutoUpdateWorker_TryRunUpdateCycleAsync_SkipsWhenCycleAlreadyActive()
        {
            var autoUpdateMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                CheckIntervalMinutes = 1
            });

            var blocker = new SemaphoreSlim(0, 1);
            var checker = new BlockingFakeUiUpdateChecker(blocker);
            var downloader = new FakeUiUpdateDownloader(new UpdateDownloadResult { Success = false });
            var installer = new FakeUiUpdateInstaller(new UpdateInstallResult { Success = true });
            var timerFactory = new FakeAutoUpdateTimerFactory(new[] { true, false });
            var logger = new TestLogger<UiAutoUpdateWorker>();

            var worker = new TestUiAutoUpdateWorker(autoUpdateMonitor, checker, downloader, installer, timerFactory, logger);

            // Start first cycle but keep it blocked inside the checker
            var firstCycle = worker.TryRunUpdateCycleAsync(CancellationToken.None);

            // Give the first cycle time to acquire the lock and enter the checker
            await Task.Delay(50);

            // Second call should be skipped because the first is still active
            var skipped = await worker.TryRunUpdateCycleAsync(CancellationToken.None);

            // Unblock the first cycle
            blocker.Release();
            var firstRan = await firstCycle;

            firstRan.Should().BeTrue();
            skipped.Should().BeFalse();
            checker.CallCount.Should().Be(1);
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

        private sealed class AssertiveUiRestartHandler : IUiRestartHandler
        {
            private readonly Action? _onRequest;

            public AssertiveUiRestartHandler(Action? onRequest = null)
            {
                _onRequest = onRequest;
            }

            public int RequestCount { get; private set; }

            public void RequestRestart()
            {
                RequestCount++;
                _onRequest?.Invoke();
            }
        }

        private sealed class FakeUiUpdateChecker : IUiUpdateChecker
        {
            private readonly ComponentUpdateCheckResult _result;

            public FakeUiUpdateChecker(ComponentUpdateCheckResult result)
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

        private sealed class BlockingFakeUiUpdateChecker : IUiUpdateChecker
        {
            private readonly SemaphoreSlim _gate;
            public int CallCount { get; private set; }

            public BlockingFakeUiUpdateChecker(SemaphoreSlim gate)
            {
                _gate = gate;
            }

            public async Task<ComponentUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
            {
                CallCount++;
                await _gate.WaitAsync(cancellationToken);
                return new ComponentUpdateCheckResult { IsUpdateAvailable = false };
            }
        }

        private sealed class FakeUiUpdateDownloader : IUiUpdateDownloader
        {
            private readonly UpdateDownloadResult _result;

            public FakeUiUpdateDownloader(UpdateDownloadResult result)
            {
                _result = result;
            }

            public Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken, IProgress<double>? progress = null)
            {
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeUiUpdateInstaller : IUiUpdateInstaller
        {
            private readonly UpdateInstallResult _result;

            public FakeUiUpdateInstaller(UpdateInstallResult result)
            {
                _result = result;
            }

            public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken, bool promptForRestart = true, IProgress<double>? progress = null)
            {
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

        private sealed class TestUiAutoUpdateWorker : UiAutoUpdateWorker
        {
            public TestUiAutoUpdateWorker(
                Microsoft.Extensions.Options.IOptionsMonitor<AutoUpdateOptions> optionsMonitor,
                IUiUpdateChecker updateChecker,
                IUiUpdateDownloader updateDownloader,
                IUiUpdateInstaller updateInstaller,
                IAutoUpdateTimerFactory timerFactory,
                ILogger<UiAutoUpdateWorker> logger)
                : base(optionsMonitor, updateChecker, updateDownloader, updateInstaller, timerFactory, logger)
            {
            }

            public Task RunAsync(CancellationToken token)
            {
                return ExecuteAsync(token);
            }
        }
    }

    internal sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    internal static class TestDirectoryFactory
    {
        public static string CreateTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "StorageWatchUiTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

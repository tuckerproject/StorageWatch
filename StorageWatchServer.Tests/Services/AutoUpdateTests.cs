using Microsoft.Extensions.Options;
using StorageWatch.Shared.Update.Models;
using StorageWatchServer.Config;
using StorageWatchServer.Services.AutoUpdate;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StorageWatchServer.Tests.Services
{
    public class AutoUpdateTests
    {
        [Fact]
        public void ServerUpdateChecker_ParseManifest_ParsesExpectedFields()
        {
            var json = "{\"version\":\"1.0.0\",\"server\":{\"version\":\"1.2.3\",\"downloadUrl\":\"https://example.com/update.zip\",\"sha256\":\"abc123\"}}";

            var manifest = ServerUpdateChecker.ParseManifest(json);

            Assert.NotNull(manifest);
            Assert.NotNull(manifest!.Server);
            Assert.Equal("1.2.3", manifest.Server!.Version);
            Assert.Equal("https://example.com/update.zip", manifest.Server.DownloadUrl);
            Assert.Equal("abc123", manifest.Server.Sha256);
        }

        [Fact]
        public async Task ServerUpdateChecker_CheckForUpdateAsync_ReturnsUpdateWhenNewerVersion()
        {
            var currentVersion = typeof(ServerUpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            var newerVersion = new Version(currentVersion.Major + 1, 0, 0, 0);
            var manifestJson = $"{{\"version\":\"1.0.0\",\"server\":{{\"version\":\"{newerVersion}\",\"downloadUrl\":\"https://example.com/update.zip\",\"sha256\":\"abc\"}}}}";
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
            var logger = new TestLogger<ServerUpdateChecker>();
            var checker = new ServerUpdateChecker(httpClient, logger, optionsMonitor);

            var result = await checker.CheckForUpdateAsync(CancellationToken.None);

            Assert.True(result.IsUpdateAvailable);
            Assert.NotNull(result.Component);
            Assert.Equal(newerVersion.ToString(), result.Component!.Version);
        }

        [Fact]
        public async Task ServerUpdateDownloader_ReturnsFailureOnHashMismatch()
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
            var logger = new TestLogger<ServerUpdateDownloader>();
            var downloader = new ServerUpdateDownloader(httpClient, logger);

            var result = await downloader.DownloadAsync(component, CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ServerUpdateInstaller_ExtractsAndCopiesFiles_TriggersRestart()
        {
            var tempSource = TestDirectoryFactory.CreateTempDirectory();
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var zipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "update.zip");
            var updaterExePath = Path.Combine(tempTarget, "StorageWatch.Updater.exe");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");
            ZipFile.CreateFromDirectory(tempSource, zipPath);
            await File.WriteAllTextAsync(updaterExePath, string.Empty);

            var launched = false;
            var gracefulStopRequested = false;
            var exitRequested = false;

            var installer = new ServerUpdateInstaller(
                new TestLogger<ServerUpdateInstaller>(),
                new FakeRestartHandler(),
                tempTarget,
                (_, _) =>
                {
                    launched = true;
                    return true;
                },
                () => gracefulStopRequested = true,
                () => exitRequested = true);

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(launched);
            Assert.True(gracefulStopRequested);
            Assert.True(exitRequested);
            Assert.False(File.Exists(Path.Combine(tempTarget, "app", "test.txt")));
        }

        [Fact]
        public async Task ServerUpdateInstaller_RequestsRestart_OnlyAfterSuccessfulInstall()
        {
            var tempSource = TestDirectoryFactory.CreateTempDirectory();
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var zipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "update.zip");
            var updaterExePath = Path.Combine(tempTarget, "StorageWatch.Updater.exe");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");
            ZipFile.CreateFromDirectory(tempSource, zipPath);
            await File.WriteAllTextAsync(updaterExePath, string.Empty);

            string? launchedExe = null;
            string? launchedArgs = null;

            var installer = new ServerUpdateInstaller(
                new TestLogger<ServerUpdateInstaller>(),
                new AssertiveServerRestartHandler(),
                tempTarget,
                (exe, args) =>
                {
                    launchedExe = exe;
                    launchedArgs = args;
                    return true;
                },
                () => { },
                () => { });

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("powershell.exe", launchedExe);
            Assert.NotNull(launchedArgs);
            Assert.Contains("Start-Process", launchedArgs, StringComparison.Ordinal);
            Assert.Contains("StorageWatch.Updater.exe", launchedArgs, StringComparison.Ordinal);
            Assert.Contains("--update-server", launchedArgs, StringComparison.Ordinal);
            Assert.Contains("--source", launchedArgs, StringComparison.Ordinal);
            Assert.Contains("--target", launchedArgs, StringComparison.Ordinal);
            Assert.Contains("--manifest", launchedArgs, StringComparison.Ordinal);
            Assert.Contains("--restart-server", launchedArgs, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ServerUpdateInstaller_DoesNotRequestRestart_WhenInstallFails()
        {
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var missingZipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "missing-update.zip");
            var launched = false;
            var gracefulStopRequested = false;
            var exitRequested = false;

            var installer = new ServerUpdateInstaller(
                new TestLogger<ServerUpdateInstaller>(),
                new AssertiveServerRestartHandler(),
                tempTarget,
                (_, _) =>
                {
                    launched = true;
                    return true;
                },
                () => gracefulStopRequested = true,
                () => exitRequested = true);

            var result = await installer.InstallAsync(missingZipPath, CancellationToken.None);

            Assert.False(result.Success);
            Assert.False(launched);
            Assert.False(gracefulStopRequested);
            Assert.False(exitRequested);
        }

        [Fact]
        public async Task ServerUpdateInstaller_RestoresBackup_WhenInstallFails()
        {
            var tempSource = TestDirectoryFactory.CreateTempDirectory();
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var zipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "update.zip");
            var updaterExePath = Path.Combine(tempTarget, "StorageWatch.Updater.exe");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");
            ZipFile.CreateFromDirectory(tempSource, zipPath);
            await File.WriteAllTextAsync(updaterExePath, string.Empty);

            var installer = new ServerUpdateInstaller(
                new TestLogger<ServerUpdateInstaller>(),
                new ThrowingRestartHandler(),
                tempTarget,
                (_, _) => false,
                () => { },
                () => { });

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Failed to launch updater executable.", result.ErrorMessage, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempTarget, "app", "test.txt")));
        }

        [Fact]
        public async Task ServerUpdateInstaller_RestoresBackupAndCleansTempDirectories_WhenRestartFails()
        {
            var tempSource = TestDirectoryFactory.CreateTempDirectory();
            var tempTarget = TestDirectoryFactory.CreateTempDirectory();
            var zipPath = Path.Combine(TestDirectoryFactory.CreateTempDirectory(), "update.zip");
            var updaterExePath = Path.Combine(tempTarget, "StorageWatch.Updater.exe");

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            var newFile = Path.Combine(tempSource, "app", "new.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");
            await File.WriteAllTextAsync(newFile, "new-file");
            ZipFile.CreateFromDirectory(tempSource, zipPath);
            await File.WriteAllTextAsync(updaterExePath, string.Empty);

            var launched = false;
            var installer = new ServerUpdateInstaller(
                new TestLogger<ServerUpdateInstaller>(),
                new ThrowingRestartHandler(),
                tempTarget,
                (_, _) =>
                {
                    launched = true;
                    return true;
                },
                () => { },
                () => { });

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(launched);
            Assert.False(File.Exists(Path.Combine(tempTarget, "app", "test.txt")));
            Assert.False(File.Exists(Path.Combine(tempTarget, "app", "new.txt")));
        }


        [Fact]
        public async Task ServerAutoUpdateWorker_UsesTimerTicksToRunUpdateCycle()
        {
            var autoUpdateMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = true,
                CheckIntervalMinutes = 1
            });

            var checker = new FakeServerUpdateChecker(new ComponentUpdateCheckResult { IsUpdateAvailable = false });
            var downloader = new FakeServerUpdateDownloader(new UpdateDownloadResult { Success = false });
            var installer = new FakeServerUpdateInstaller(new UpdateInstallResult { Success = true });
            var timerFactory = new FakeAutoUpdateTimerFactory(new[] { true, false });
            var logger = new TestLogger<ServerAutoUpdateWorker>();

            var worker = new TestServerAutoUpdateWorker(autoUpdateMonitor, checker, downloader, installer, timerFactory, logger);

            await worker.RunAsync(CancellationToken.None);

            Assert.Equal(1, checker.CallCount);
        }

        [Fact]
        public async Task ServerAutoUpdateWorker_DoesNotRunWhenDisabled()
        {
            var autoUpdateMonitor = new TestOptionsMonitor<AutoUpdateOptions>(new AutoUpdateOptions
            {
                Enabled = false,
                CheckIntervalMinutes = 1
            });

            var checker = new FakeServerUpdateChecker(new ComponentUpdateCheckResult { IsUpdateAvailable = false });
            var downloader = new FakeServerUpdateDownloader(new UpdateDownloadResult { Success = false });
            var installer = new FakeServerUpdateInstaller(new UpdateInstallResult { Success = true });
            var timerFactory = new FakeAutoUpdateTimerFactory(new[] { true, false });
            var logger = new TestLogger<ServerAutoUpdateWorker>();

            var worker = new TestServerAutoUpdateWorker(autoUpdateMonitor, checker, downloader, installer, timerFactory, logger);

            await worker.RunAsync(CancellationToken.None);

            Assert.Equal(0, checker.CallCount);
        }

        [Fact]
        public void ServerRestartHandler_BuildRestartHelperScript_UsesScmRestartFlow()
        {
            var method = typeof(ServerRestartHandler).GetMethod("BuildRestartHelperScript", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            var script = Assert.IsType<string>(method!.Invoke(null, new object[]
            {
                "StorageWatchServer",
                TimeSpan.FromSeconds(30),
                @"C:\Logs\server-restart.log"
            }));

            Assert.Contains("Wait-ForState", script, StringComparison.Ordinal);
            Assert.Contains("sc.exe stop", script, StringComparison.Ordinal);
            Assert.Contains("sc.exe start", script, StringComparison.Ordinal);
            Assert.Contains("STOPPED", script, StringComparison.Ordinal);
            Assert.Contains("RUNNING", script, StringComparison.Ordinal);
            Assert.Contains("server-restart.log", script, StringComparison.Ordinal);
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

        private sealed class FakeRestartHandler : IServerRestartHandler
        {
            public bool RestartRequested { get; private set; }

            public void RequestRestart()
            {
                RestartRequested = true;
            }
        }

        private sealed class ThrowingRestartHandler : IServerRestartHandler
        {
            public void RequestRestart()
            {
                throw new InvalidOperationException("Restart failed");
            }
        }

        private sealed class AssertiveServerRestartHandler : IServerRestartHandler
        {
            private readonly Action? _onRequest;

            public AssertiveServerRestartHandler(Action? onRequest = null)
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

        private sealed class FakeServerUpdateChecker : IServerUpdateChecker
        {
            private readonly ComponentUpdateCheckResult _result;

            public FakeServerUpdateChecker(ComponentUpdateCheckResult result)
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

        private sealed class FakeServerUpdateDownloader : IServerUpdateDownloader
        {
            private readonly UpdateDownloadResult _result;

            public FakeServerUpdateDownloader(UpdateDownloadResult result)
            {
                _result = result;
            }

            public Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken)
            {
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeServerUpdateInstaller : IServerUpdateInstaller
        {
            private readonly UpdateInstallResult _result;

            public FakeServerUpdateInstaller(UpdateInstallResult result)
            {
                _result = result;
            }

            public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
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

        private sealed class TestServerAutoUpdateWorker : ServerAutoUpdateWorker
        {
            public TestServerAutoUpdateWorker(
                IOptionsMonitor<AutoUpdateOptions> optionsMonitor,
                IServerUpdateChecker updateChecker,
                IServerUpdateDownloader updateDownloader,
                IServerUpdateInstaller updateInstaller,
                IAutoUpdateTimerFactory timerFactory,
                Microsoft.Extensions.Logging.ILogger<ServerAutoUpdateWorker> logger)
                : base(optionsMonitor, updateChecker, updateDownloader, updateInstaller, timerFactory, logger)
            {
            }

            public Task RunAsync(CancellationToken token)
            {
                return ExecuteAsync(token);
            }
        }

        private static HashSet<string> SnapshotTempChildDirectories(string folderName)
        {
            var root = Path.Combine(Path.GetTempPath(), folderName);
            return new HashSet<string>(
                Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>, IOptions<T> where T : class
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
            Value = currentValue;
        }

        public T CurrentValue { get; private set; }

        public T Value { get; private set; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => new NoOpDisposable();

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    internal sealed class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
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
            var dir = Path.Combine(Path.GetTempPath(), "StorageWatchServerTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

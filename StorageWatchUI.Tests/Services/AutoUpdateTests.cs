using FluentAssertions;
using Microsoft.Extensions.Logging;
using StorageWatchUI.Config;
using StorageWatchUI.Models;
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
            var manifestJson = "{\"version\":\"1.0.0\",\"ui\":{\"version\":\"2.0.0\",\"downloadUrl\":\"https://example.com/update.zip\",\"sha256\":\"abc\"}}";
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
            result.Component!.Version.Should().Be("2.0.0");
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

            var sourceFile = Path.Combine(tempSource, "app", "test.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            await File.WriteAllTextAsync(sourceFile, "updated");

            ZipFile.CreateFromDirectory(tempSource, zipPath);

            var logger = new TestLogger<UiUpdateInstaller>();
            var prompt = new FakeRestartPrompter(true);
            var restartHandler = new FakeRestartHandler();
            var installer = new UiUpdateInstaller(logger, prompt, restartHandler, tempTarget);

            var result = await installer.InstallAsync(zipPath, CancellationToken.None);

            var targetFile = Path.Combine(tempTarget, "app", "test.txt");
            result.Success.Should().BeTrue();
            File.Exists(targetFile).Should().BeTrue();
            var contents = await File.ReadAllTextAsync(targetFile);
            contents.Should().Be("updated");
            restartHandler.RestartRequested.Should().BeTrue();
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

        private sealed class FakeUiUpdateDownloader : IUiUpdateDownloader
        {
            private readonly UpdateDownloadResult _result;

            public FakeUiUpdateDownloader(UpdateDownloadResult result)
            {
                _result = result;
            }

            public Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken)
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

using StorageWatchUI.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchUI.Services.AutoUpdate
{
    public interface IUiAutoUpdateWorker
    {
        void Start();
        Task StopAsync();
    }

    public class UiAutoUpdateWorker : IUiAutoUpdateWorker
    {
        private readonly IOptionsMonitor<AutoUpdateOptions> _optionsMonitor;
        private readonly IUiUpdateChecker _updateChecker;
        private readonly IUiUpdateDownloader _updateDownloader;
        private readonly IUiUpdateInstaller _updateInstaller;
        private readonly IAutoUpdateTimerFactory _timerFactory;
        private readonly ILogger<UiAutoUpdateWorker> _logger;
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        public UiAutoUpdateWorker(
            IOptionsMonitor<AutoUpdateOptions> optionsMonitor,
            IUiUpdateChecker updateChecker,
            IUiUpdateDownloader updateDownloader,
            IUiUpdateInstaller updateInstaller,
            IAutoUpdateTimerFactory timerFactory,
            ILogger<UiAutoUpdateWorker> logger)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
            _updateDownloader = updateDownloader ?? throw new ArgumentNullException(nameof(updateDownloader));
            _updateInstaller = updateInstaller ?? throw new ArgumentNullException(nameof(updateInstaller));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start()
        {
            if (_runTask != null)
                return;

            _cts = new CancellationTokenSource();
            _runTask = Task.Run(() => ExecuteAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            if (_cts == null)
                return;

            _cts.Cancel();

            if (_runTask != null)
            {
                try
                {
                    await _runTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        protected async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = _optionsMonitor.CurrentValue;
            if (!options.Enabled)
            {
                _logger.LogInformation("[AUTOUPDATE] UI auto-update is disabled via configuration.");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, options.CheckIntervalMinutes));
            await using var timer = _timerFactory.Create(interval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunUpdateCycleAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        internal async Task RunUpdateCycleAsync(CancellationToken stoppingToken)
        {
            try
            {
                var result = await _updateChecker.CheckForUpdateAsync(stoppingToken);
                if (!result.IsUpdateAvailable || result.Component == null)
                {
                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                        _logger.LogWarning("[AUTOUPDATE] UI update check failed: {Error}", result.ErrorMessage);
                    else
                        _logger.LogInformation("[AUTOUPDATE] No UI updates available.");
                    return;
                }

                _logger.LogInformation("[AUTOUPDATE] UI update available: {Version}", result.Component.Version);

                var download = await _updateDownloader.DownloadAsync(result.Component, stoppingToken);
                if (!download.Success || string.IsNullOrWhiteSpace(download.FilePath))
                {
                    _logger.LogWarning("[AUTOUPDATE] UI download failed: {Error}", download.ErrorMessage);
                    return;
                }

                var install = await _updateInstaller.InstallAsync(download.FilePath, stoppingToken);
                if (!install.Success)
                {
                    _logger.LogWarning("[AUTOUPDATE] UI install failed: {Error}", install.ErrorMessage);
                    return;
                }

                _logger.LogInformation("[AUTOUPDATE] UI update installed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] UI update cycle failed");
            }
        }
    }
}

using StorageWatchServer.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Services.AutoUpdate
{
    public class ServerAutoUpdateWorker : BackgroundService
    {
        private readonly IOptionsMonitor<AutoUpdateOptions> _optionsMonitor;
        private readonly IServerUpdateChecker _updateChecker;
        private readonly IServerUpdateDownloader _updateDownloader;
        private readonly IServerUpdateInstaller _updateInstaller;
        private readonly IAutoUpdateTimerFactory _timerFactory;
        private readonly ILogger<ServerAutoUpdateWorker> _logger;

        public ServerAutoUpdateWorker(
            IOptionsMonitor<AutoUpdateOptions> optionsMonitor,
            IServerUpdateChecker updateChecker,
            IServerUpdateDownloader updateDownloader,
            IServerUpdateInstaller updateInstaller,
            IAutoUpdateTimerFactory timerFactory,
            ILogger<ServerAutoUpdateWorker> logger)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
            _updateDownloader = updateDownloader ?? throw new ArgumentNullException(nameof(updateDownloader));
            _updateInstaller = updateInstaller ?? throw new ArgumentNullException(nameof(updateInstaller));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = _optionsMonitor.CurrentValue;
            if (!options.Enabled)
            {
                _logger.LogInformation("[AUTOUPDATE] Server auto-update is disabled via configuration.");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, options.CheckIntervalMinutes));
            await using var timer = _timerFactory.Create(interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunUpdateCycleAsync(stoppingToken);
            }
        }

        public async Task RunUpdateCycleAsync(CancellationToken stoppingToken)
        {
            try
            {
                var result = await _updateChecker.CheckForUpdateAsync(stoppingToken);
                if (!result.IsUpdateAvailable || result.Component == null)
                {
                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                        _logger.LogWarning("[AUTOUPDATE] Server update check failed: {Error}", result.ErrorMessage);
                    else
                        _logger.LogInformation("[AUTOUPDATE] No server updates available.");
                    return;
                }

                _logger.LogInformation("[AUTOUPDATE] Server update available: {Version}", result.Component.Version);

                var download = await _updateDownloader.DownloadAsync(result.Component, stoppingToken);
                if (!download.Success || string.IsNullOrWhiteSpace(download.FilePath))
                {
                    _logger.LogWarning("[AUTOUPDATE] Server download failed: {Error}", download.ErrorMessage);
                    return;
                }

                var install = await _updateInstaller.InstallAsync(download.FilePath, stoppingToken);
                if (!install.Success)
                {
                    _logger.LogWarning("[AUTOUPDATE] Server install failed: {Error}", install.ErrorMessage);
                    return;
                }

                _logger.LogInformation("[AUTOUPDATE] Server update installed. Restart triggered.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Server update cycle failed");
            }
        }
    }
}

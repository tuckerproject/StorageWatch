using StorageWatch.Config.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.AutoUpdate
{
    public class ServiceAutoUpdateWorker : BackgroundService
    {
        private readonly IOptionsMonitor<AutoUpdateOptions> _optionsMonitor;
        private readonly IServiceUpdateChecker _updateChecker;
        private readonly IServiceUpdateDownloader _updateDownloader;
        private readonly IServiceUpdateInstaller _updateInstaller;
        private readonly IAutoUpdateTimerFactory _timerFactory;
        private readonly ILogger<ServiceAutoUpdateWorker> _logger;

        public ServiceAutoUpdateWorker(
            IOptionsMonitor<AutoUpdateOptions> optionsMonitor,
            IServiceUpdateChecker updateChecker,
            IServiceUpdateDownloader updateDownloader,
            IServiceUpdateInstaller updateInstaller,
            IAutoUpdateTimerFactory timerFactory,
            ILogger<ServiceAutoUpdateWorker> logger)
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
                _logger.LogInformation("[AUTOUPDATE] Service auto-update is disabled via configuration.");
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

        public async Task RunUpdateCycleAsync(CancellationToken stoppingToken)
        {
            try
            {
                var result = await _updateChecker.CheckForUpdateAsync(stoppingToken);
                if (!result.IsUpdateAvailable || result.Component == null)
                {
                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                        _logger.LogWarning("[AUTOUPDATE] Service update check failed: {Error}", result.ErrorMessage);
                    else
                        _logger.LogInformation("[AUTOUPDATE] No service updates available.");
                    return;
                }

                _logger.LogInformation("[AUTOUPDATE] Service update available: {Version}", result.Component.Version);

                var download = await _updateDownloader.DownloadAsync(result.Component, stoppingToken);
                if (!download.Success || string.IsNullOrWhiteSpace(download.FilePath))
                {
                    _logger.LogWarning("[AUTOUPDATE] Service download failed: {Error}", download.ErrorMessage);
                    return;
                }

                var install = await _updateInstaller.InstallAsync(download.FilePath, stoppingToken);
                if (!install.Success)
                {
                    _logger.LogWarning("[AUTOUPDATE] Service install failed: {Error}", install.ErrorMessage);
                    return;
                }

                _logger.LogInformation("[AUTOUPDATE] Service update installed. Restart triggered.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Service update cycle failed");
            }
        }
    }
}

using StorageWatch.Config.Options;
using StorageWatch.Services.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.AutoUpdate
{
    public class AutoUpdateWorker : BackgroundService
    {
        private readonly IOptionsMonitor<StorageWatchOptions> _storageOptionsMonitor;
        private readonly IOptionsMonitor<AutoUpdateOptions> _autoUpdateOptionsMonitor;
        private readonly IServiceUpdateChecker _serviceUpdateChecker;
        private readonly IServiceUpdateDownloader _serviceUpdateDownloader;
        private readonly IServiceUpdateInstaller _serviceUpdateInstaller;
        private readonly IPluginUpdateChecker _pluginUpdateChecker;
        private readonly IPluginUpdateDownloader _pluginUpdateDownloader;
        private readonly IPluginUpdateInstaller _pluginUpdateInstaller;
        private readonly IAutoUpdateTimerFactory _timerFactory;
        private readonly RollingFileLogger _logger;

        public AutoUpdateWorker(
            IOptionsMonitor<StorageWatchOptions> storageOptionsMonitor,
            IOptionsMonitor<AutoUpdateOptions> autoUpdateOptionsMonitor,
            IServiceUpdateChecker serviceUpdateChecker,
            IServiceUpdateDownloader serviceUpdateDownloader,
            IServiceUpdateInstaller serviceUpdateInstaller,
            IPluginUpdateChecker pluginUpdateChecker,
            IPluginUpdateDownloader pluginUpdateDownloader,
            IPluginUpdateInstaller pluginUpdateInstaller,
            IAutoUpdateTimerFactory timerFactory,
            RollingFileLogger logger)
        {
            _storageOptionsMonitor = storageOptionsMonitor ?? throw new ArgumentNullException(nameof(storageOptionsMonitor));
            _autoUpdateOptionsMonitor = autoUpdateOptionsMonitor ?? throw new ArgumentNullException(nameof(autoUpdateOptionsMonitor));
            _serviceUpdateChecker = serviceUpdateChecker ?? throw new ArgumentNullException(nameof(serviceUpdateChecker));
            _serviceUpdateDownloader = serviceUpdateDownloader ?? throw new ArgumentNullException(nameof(serviceUpdateDownloader));
            _serviceUpdateInstaller = serviceUpdateInstaller ?? throw new ArgumentNullException(nameof(serviceUpdateInstaller));
            _pluginUpdateChecker = pluginUpdateChecker ?? throw new ArgumentNullException(nameof(pluginUpdateChecker));
            _pluginUpdateDownloader = pluginUpdateDownloader ?? throw new ArgumentNullException(nameof(pluginUpdateDownloader));
            _pluginUpdateInstaller = pluginUpdateInstaller ?? throw new ArgumentNullException(nameof(pluginUpdateInstaller));
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var storageOptions = _storageOptionsMonitor.CurrentValue;
            if (storageOptions.Mode == StorageWatchMode.Server)
            {
                _logger.Log("[AUTOUPDATE] Auto-update is disabled in Server mode.");
                return;
            }

            var autoUpdateOptions = _autoUpdateOptionsMonitor.CurrentValue;
            if (!autoUpdateOptions.Enabled)
            {
                _logger.Log("[AUTOUPDATE] Auto-update is disabled via configuration.");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, autoUpdateOptions.CheckIntervalMinutes));
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
                await RunServiceUpdateAsync(stoppingToken);
                await RunPluginUpdatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Log($"[AUTOUPDATE] Update cycle failed: {ex}");
            }
        }

        private async Task RunServiceUpdateAsync(CancellationToken stoppingToken)
        {
            var result = await _serviceUpdateChecker.CheckForUpdateAsync(stoppingToken);
            if (!result.IsUpdateAvailable || result.Component == null)
            {
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                    _logger.Log($"[AUTOUPDATE] Update check failed: {result.ErrorMessage}");
                else
                    _logger.Log("[AUTOUPDATE] No service updates available.");
                return;
            }

            _logger.Log($"[AUTOUPDATE] Service update available: {result.Component.Version}");

            var download = await _serviceUpdateDownloader.DownloadAsync(result.Component, stoppingToken);
            if (!download.Success || string.IsNullOrWhiteSpace(download.FilePath))
            {
                _logger.Log($"[AUTOUPDATE] Service download failed: {download.ErrorMessage}");
                return;
            }

            var install = await _serviceUpdateInstaller.InstallAsync(download.FilePath, stoppingToken);
            if (!install.Success)
            {
                _logger.Log($"[AUTOUPDATE] Service install failed: {install.ErrorMessage}");
                return;
            }

            _logger.Log("[AUTOUPDATE] Service update installed. Restart triggered.");
        }

        private async Task RunPluginUpdatesAsync(CancellationToken stoppingToken)
        {
            var result = await _pluginUpdateChecker.CheckForUpdatesAsync(stoppingToken);
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                _logger.Log($"[AUTOUPDATE] Plugin check failed: {result.ErrorMessage}");
                return;
            }

            if (result.Updates.Count == 0)
            {
                _logger.Log("[AUTOUPDATE] No plugin updates available.");
                return;
            }

            foreach (var plugin in result.Updates)
            {
                _logger.Log($"[AUTOUPDATE] Plugin update available: {plugin.Name} {plugin.Version}");

                var download = await _pluginUpdateDownloader.DownloadAsync(plugin, stoppingToken);
                if (!download.Success || string.IsNullOrWhiteSpace(download.FilePath))
                {
                    _logger.Log($"[AUTOUPDATE] Plugin download failed ({plugin.Name}): {download.ErrorMessage}");
                    continue;
                }

                var install = await _pluginUpdateInstaller.InstallAsync(download.FilePath, stoppingToken);
                if (!install.Success)
                {
                    _logger.Log($"[AUTOUPDATE] Plugin install failed ({plugin.Name}): {install.ErrorMessage}");
                    continue;
                }

                _logger.Log($"[AUTOUPDATE] Plugin update installed: {plugin.Name}");
            }
        }
    }
}

using StorageWatch.Shared.Update.Models;
using StorageWatchUI.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchUI.Services.AutoUpdate
{
    public sealed class UiUpdateProgressInfo
    {
        public string Status { get; init; } = string.Empty;
        public double ProgressPercent { get; init; }
        public bool IsIndeterminate { get; init; }
    }

    public interface IUiAutoUpdateWorker
    {
        void Start();
        Task StopAsync();
        bool IsCycleActive { get; }
        Task<bool> TryRunUpdateCycleAsync(CancellationToken cancellationToken = default);
        Task<bool> TryInstallAvailableUpdateAsync(CancellationToken cancellationToken = default);
        event EventHandler<ComponentUpdateCheckResult>? UpdateCheckCompleted;
        event EventHandler<UiUpdateProgressInfo>? UpdateProgressChanged;
        event EventHandler<UpdateInstallResult>? UpdateInstallCompleted;
        event EventHandler? RestartPromptRequested;
    }

    public class UiAutoUpdateWorker : IUiAutoUpdateWorker
    {
        private readonly IOptionsMonitor<AutoUpdateOptions> _optionsMonitor;
        private readonly IUiUpdateChecker _updateChecker;
        private readonly IUiUpdateDownloader _updateDownloader;
        private readonly IUiUpdateInstaller _updateInstaller;
        private readonly IAutoUpdateTimerFactory _timerFactory;
        private readonly ILogger<UiAutoUpdateWorker> _logger;
        private readonly SemaphoreSlim _cycleLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _runTask;
        private ComponentUpdateInfo? _pendingComponent;

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

        public event EventHandler<ComponentUpdateCheckResult>? UpdateCheckCompleted;
        public event EventHandler<UiUpdateProgressInfo>? UpdateProgressChanged;
        public event EventHandler<UpdateInstallResult>? UpdateInstallCompleted;
        public event EventHandler? RestartPromptRequested;

        public bool IsCycleActive => _cycleLock.CurrentCount == 0;

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
                await TryRunUpdateCycleAsync(stoppingToken);

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await TryRunUpdateCycleAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public async Task<bool> TryRunUpdateCycleAsync(CancellationToken cancellationToken = default)
        {
            if (!await _cycleLock.WaitAsync(0, cancellationToken))
            {
                _logger.LogInformation("[AUTOUPDATE] Skipping update check — cycle already active.");
                return false;
            }

            try
            {
                var result = await RunUpdateCheckAsync(cancellationToken);
                UpdateCheckCompleted?.Invoke(this, result);
                return true;
            }
            finally
            {
                _cycleLock.Release();
            }
        }

        public async Task<bool> TryInstallAvailableUpdateAsync(CancellationToken cancellationToken = default)
        {
            if (!await _cycleLock.WaitAsync(0, cancellationToken))
            {
                _logger.LogInformation("[AUTOUPDATE] Skipping update install — cycle already active.");
                return false;
            }

            try
            {
                var component = _pendingComponent;
                if (component == null)
                {
                    var check = await RunUpdateCheckAsync(cancellationToken);
                    UpdateCheckCompleted?.Invoke(this, check);
                    component = check.Component;
                }

                if (component == null)
                {
                    UpdateInstallCompleted?.Invoke(this, new UpdateInstallResult
                    {
                        Success = false,
                        ErrorMessage = "No update is currently available."
                    });
                    return false;
                }

                var downloadProgress = new Progress<double>(p =>
                    UpdateProgressChanged?.Invoke(this, new UiUpdateProgressInfo
                    {
                        Status = "Downloading update...",
                        ProgressPercent = Math.Clamp(p * 0.7, 0, 70),
                        IsIndeterminate = false
                    }));

                UpdateProgressChanged?.Invoke(this, new UiUpdateProgressInfo
                {
                    Status = "Downloading update...",
                    ProgressPercent = 0,
                    IsIndeterminate = true
                });

                var download = await _updateDownloader.DownloadAsync(component, cancellationToken, downloadProgress);
                if (!download.Success || string.IsNullOrWhiteSpace(download.FilePath))
                {
                    UpdateInstallCompleted?.Invoke(this, new UpdateInstallResult
                    {
                        Success = false,
                        ErrorMessage = download.ErrorMessage ?? "Download failed"
                    });
                    return false;
                }

                var installProgress = new Progress<double>(p =>
                    UpdateProgressChanged?.Invoke(this, new UiUpdateProgressInfo
                    {
                        Status = "Installing update...",
                        ProgressPercent = 70 + Math.Clamp(p * 0.3, 0, 30),
                        IsIndeterminate = false
                    }));

                UpdateProgressChanged?.Invoke(this, new UiUpdateProgressInfo
                {
                    Status = "Installing update...",
                    ProgressPercent = 70,
                    IsIndeterminate = true
                });

                var install = await _updateInstaller.InstallAsync(download.FilePath, cancellationToken, promptForRestart: false, progress: installProgress);

                UpdateInstallCompleted?.Invoke(this, install);

                if (install.Success)
                {
                    UpdateProgressChanged?.Invoke(this, new UiUpdateProgressInfo
                    {
                        Status = "Update installed successfully",
                        ProgressPercent = 100,
                        IsIndeterminate = false
                    });

                    _pendingComponent = null;
                    RestartPromptRequested?.Invoke(this, EventArgs.Empty);
                }

                return install.Success;
            }
            finally
            {
                _cycleLock.Release();
            }
        }

        private async Task<ComponentUpdateCheckResult> RunUpdateCheckAsync(CancellationToken stoppingToken)
        {
            try
            {
                var result = await _updateChecker.CheckForUpdateAsync(stoppingToken);
                if (!result.IsUpdateAvailable || result.Component == null)
                {
                    _pendingComponent = null;
                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                        _logger.LogWarning("[AUTOUPDATE] UI update check failed: {Error}", result.ErrorMessage);
                    else
                        _logger.LogInformation("[AUTOUPDATE] No UI updates available.");
                    return result;
                }

                _pendingComponent = result.Component;
                _logger.LogInformation("[AUTOUPDATE] UI update available: {Version}", result.Component.Version);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] UI update cycle failed");
                return new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}

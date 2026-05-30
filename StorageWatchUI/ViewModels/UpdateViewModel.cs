using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StorageWatch.Shared.Update.Models;
using StorageWatchUI.Communication;
using StorageWatchUI.Config;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.Views;
using System.Windows;

namespace StorageWatchUI.ViewModels;

public class UpdateViewModel : ViewModelBase
{
    private bool _isUpdateAvailable;
    private string _currentVersion = string.Empty;
    private string _currentUiVersion = string.Empty;
    private string _currentAgentVersion = "0.0.0.0";
    private string _currentServerVersion = "0.0.0.0";
    private string _latestVersion = string.Empty;
    private string _releaseNotes = string.Empty;
    private bool _isUpdateInProgress;
    private bool _isRestartRequired;
    private string _updateStatus = string.Empty;
    private double _updateProgress;
    private bool _isBannerVisible;
    private readonly IUiUpdateChecker _updateChecker;
    private readonly ServiceCommunicationClient _serviceCommunicationClient;
    private readonly IUiAutoUpdateWorker _autoUpdateWorker;
    private readonly IUiUpdateUserSettingsStore _userSettingsStore;
    private readonly IOptionsMonitor<AutoUpdateOptions> _autoUpdateOptions;
    private readonly ILogger<UpdateViewModel> _logger;
    private CancellationTokenSource? _updateCts;
    private UpdateProgressViewModel? _progressViewModel;
    private UpdateProgressDialog? _progressDialog;
    private string? _skippedVersion;
    private DateTimeOffset? _snoozeUntilUtc;
    private UpdateManifest _latestManifest = new();

    public UpdateViewModel(
        IUiUpdateChecker updateChecker,
        ServiceCommunicationClient serviceCommunicationClient,
        IUiUpdateDownloader updateDownloader,
        IUiUpdateInstaller updateInstaller,
        IUiAutoUpdateWorker autoUpdateWorker,
        IUiUpdateUserSettingsStore userSettingsStore,
        IOptionsMonitor<AutoUpdateOptions> autoUpdateOptions,
        ILogger<UpdateViewModel> logger)
    {
        _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
        _serviceCommunicationClient = serviceCommunicationClient ?? throw new ArgumentNullException(nameof(serviceCommunicationClient));
        _autoUpdateWorker = autoUpdateWorker ?? throw new ArgumentNullException(nameof(autoUpdateWorker));
        _userSettingsStore = userSettingsStore ?? throw new ArgumentNullException(nameof(userSettingsStore));
        _autoUpdateOptions = autoUpdateOptions ?? throw new ArgumentNullException(nameof(autoUpdateOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _autoUpdateWorker.UpdateCheckCompleted += OnUpdateCheckCompleted;
        _autoUpdateWorker.UpdateInstallCompleted += OnUpdateInstallCompleted;

        CurrentUiVersion = GetCurrentVersion();
        CurrentVersion = CurrentUiVersion;
        _skippedVersion = _userSettingsStore.GetSkippedVersion();

        CheckForUpdatesCommand = new RelayCommand(CheckForUpdatesAsync);
        BeginUpdateCommand = new RelayCommand(BeginUpdateAsync);
        CancelUpdateCommand = new RelayCommand(CancelUpdate, () => IsUpdateInProgress);
        RestartNowCommand = new RelayCommand(RestartNow, () => IsRestartRequired);
        DismissBannerCommand = new RelayCommand(DismissBanner);
        RemindMeLaterCommand = new RelayCommand(RemindMeLater);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    public string CurrentVersion
    {
        get => _currentVersion;
        set => SetProperty(ref _currentVersion, value);
    }

    public string CurrentUiVersion
    {
        get => _currentUiVersion;
        set => SetProperty(ref _currentUiVersion, value);
    }

    public string CurrentAgentVersion
    {
        get => _currentAgentVersion;
        set => SetProperty(ref _currentAgentVersion, value);
    }

    public string CurrentServerVersion
    {
        get => _currentServerVersion;
        set => SetProperty(ref _currentServerVersion, value);
    }

    public string LatestVersion
    {
        get => _latestVersion;
        set => SetProperty(ref _latestVersion, value);
    }

    public string ReleaseNotes
    {
        get => _releaseNotes;
        set => SetProperty(ref _releaseNotes, value);
    }

    public bool IsUpdateInProgress
    {
        get => _isUpdateInProgress;
        set => SetProperty(ref _isUpdateInProgress, value);
    }

    public bool IsRestartRequired
    {
        get => _isRestartRequired;
        set => SetProperty(ref _isRestartRequired, value);
    }

    public string UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    public double UpdateProgress
    {
        get => _updateProgress;
        set => SetProperty(ref _updateProgress, value);
    }

    public bool IsBannerVisible
    {
        get => _isBannerVisible;
        set => SetProperty(ref _isBannerVisible, value);
    }

    public ICommand CheckForUpdatesCommand { get; }
    public ICommand BeginUpdateCommand { get; }
    public ICommand CancelUpdateCommand { get; }
    public ICommand RestartNowCommand { get; }
    public ICommand DismissBannerCommand { get; }
    public ICommand RemindMeLaterCommand { get; }

    private async void CheckForUpdatesAsync()
    {
        if (_autoUpdateWorker.IsCycleActive)
        {
            _logger.LogInformation("[AUTOUPDATE] Skipping manual check — background update cycle is already active.");
            UpdateStatus = "Update check already in progress.";
            return;
        }

        try
        {
            UpdateStatus = "Checking for updates...";
            _updateCts = new CancellationTokenSource();
            await _autoUpdateWorker.TryRunUpdateCycleAsync(_updateCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update check cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            UpdateStatus = $"Error: {ex.Message}";
        }
    }

    private async void BeginUpdateAsync()
    {
        if (!IsUpdateAvailable)
            return;

        await PerformUpdateAsync();
    }

    private async Task PerformUpdateAsync()
    {
        if (_autoUpdateWorker.IsCycleActive)
        {
            _logger.LogInformation("[AUTOUPDATE] Skipping manual install — background update cycle is already active.");
            UpdateStatus = "Update already in progress.";
            return;
        }

        var unifiedSucceeded = false;
        var unifiedFailed = false;

        try
        {
            _logger.LogInformation("[UPDATE] Starting updater handoff flow");

            IsUpdateInProgress = true;
            UpdateProgress = 0;
            _updateCts = new CancellationTokenSource();

            _progressViewModel = new UpdateProgressViewModel
            {
                StatusText = "Preparing update...",
                IsIndeterminate = true,
                Progress = 0
            };

            _progressDialog = new UpdateProgressDialog(_progressViewModel);
            _progressViewModel.CancelRequested += (s, e) => CancelUpdate();
            _progressDialog.Show();

            var startedViaAgent = await TryRunUnifiedInstallViaAgentAsync(_updateCts.Token);
            if (!startedViaAgent)
            {
                unifiedFailed = true;
                if (string.IsNullOrWhiteSpace(UpdateStatus))
                    UpdateStatus = "Unable to start unified install via Agent.";

                await RefreshVersionsAsync();
                return;
            }

            var lastResult = await _serviceCommunicationClient.GetLastUnifiedInstallResultAsync(_updateCts.Token);
            if (lastResult?.Success == true)
            {
                unifiedSucceeded = true;
                _logger.LogInformation("[UPDATE] Agent unified orchestration completed successfully");
            }
            else
            {
                unifiedFailed = true;
                UpdateStatus = lastResult?.ErrorMessage ?? "Update failed.";
            }

            await RefreshVersionsAsync();
        }
        catch (OperationCanceledException)
        {
            unifiedFailed = true;
            _logger.LogInformation("Update cancelled");
            UpdateStatus = "Update cancelled";
        }
        catch (Exception ex)
        {
            unifiedFailed = true;
            _logger.LogError(ex, "Error during update");
            UpdateStatus = $"Error: {ex.Message}";
            MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsUpdateInProgress = false;
            CloseProgressDialog();

            _updateCts?.Dispose();
            _updateCts = null;

            UpdateProgress = 0;
            ReevaluateBannerVisibility(unifiedSucceeded, unifiedFailed);
        }
    }

    private async Task<bool> TryRunUnifiedInstallViaAgentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installRequest = new UnifiedInstallUpdateRequest
            {
                UpdateAll = true,
                Force = false,
                RequestedBy = "LocalUI"
            };

            var startResult = await _serviceCommunicationClient.StartUnifiedInstallAsync(installRequest, cancellationToken);
            if (startResult == null)
                return false;

            if (!string.IsNullOrWhiteSpace(startResult.ErrorMessage))
            {
                UpdateStatus = startResult.ErrorMessage;
                return true;
            }

            var pollAttempts = 0;
            while (pollAttempts < 120)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var progress = await _serviceCommunicationClient.GetUnifiedInstallProgressAsync(cancellationToken);
                if (progress != null)
                {
                    UpdateStatus = string.IsNullOrWhiteSpace(progress.Status)
                        ? "Applying updates..."
                        : progress.Status;

                    if (_progressViewModel != null)
                    {
                        _progressViewModel.StatusText = UpdateStatus;
                        _progressViewModel.Progress = progress.ProgressPercent;
                        _progressViewModel.IsIndeterminate = progress.IsIndeterminate;
                    }

                    if (string.Equals(progress.Phase, "completed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(progress.Phase, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }

                pollAttempts++;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[UPDATE] Agent unified orchestration path unavailable.");
            return false;
        }
    }

    private async void OnUpdateCheckCompleted(object? sender, ComponentUpdateCheckResult result)
    {
        if (await TryApplyAgentUnifiedStatusAsync(CancellationToken.None))
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (_snoozeUntilUtc.HasValue && DateTimeOffset.UtcNow >= _snoozeUntilUtc.Value)
            {
                _snoozeUntilUtc = null;
            }

            IsUpdateAvailable = false;
            IsBannerVisible = false;
            UpdateStatus = "Unified update status unavailable.";
            ReevaluateBannerVisibility(unifiedUpdateSucceeded: false, unifiedUpdateFailed: false);
        });
    }

    private void OnUpdateInstallCompleted(object? sender, UpdateInstallResult result)
    {
        RunOnUiThread(() =>
        {
            IsUpdateInProgress = false;

            if (result.Success)
            {
                IsUpdateAvailable = false;
                IsBannerVisible = false;
                UpdateStatus = "StorageWatch is restarting to apply updates…";
                UpdateProgress = 100;
                return;
            }

            UpdateStatus = $"Update failed: {result.ErrorMessage}";
            MessageBox.Show($"Update failed: {result.ErrorMessage}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CloseProgressDialog();
        });
    }

    private void RestartNow()
    {
        IsRestartRequired = false;
        UpdateStatus = "Restart is delegated to updater executable.";
    }

    private void CloseProgressDialog()
    {
        if (_progressDialog == null)
            return;

        _progressDialog.Close();
        _progressDialog = null;
        _progressViewModel = null;
    }

    private async Task RefreshVersionsAsync()
    {
        CurrentUiVersion = GetCurrentVersion();
        CurrentVersion = CurrentUiVersion;

        if (!await TryApplyAgentUnifiedStatusAsync(CancellationToken.None))
        {
            IsUpdateAvailable = false;
            IsBannerVisible = false;
            UpdateStatus = "Unified update status unavailable.";
        }

        ReevaluateBannerVisibility(unifiedUpdateSucceeded: false, unifiedUpdateFailed: false);
    }

    private async Task<bool> TryApplyAgentUnifiedStatusAsync(CancellationToken cancellationToken)
    {
        UnifiedUpdateStatusInfo? unified;
        try
        {
            unified = await _serviceCommunicationClient.GetUnifiedUpdateStatusAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AUTOUPDATE] Failed to query Agent unified update status via IPC.");
            return false;
        }

        if (unified == null)
            return false;

        var components = unified.Components
            .Where(c => !string.IsNullOrWhiteSpace(c.Component))
            .ToDictionary(c => c.Component.Trim().ToLowerInvariant(), c => c, StringComparer.OrdinalIgnoreCase);

        CurrentAgentVersion = components.TryGetValue("agent", out var agent) ? agent.CurrentVersion : "0.0.0.0";
        CurrentServerVersion = components.TryGetValue("server", out var server) ? server.CurrentVersion : "0.0.0.0";
        CurrentUiVersion = components.TryGetValue("ui", out var ui) ? ui.CurrentVersion : CurrentUiVersion;
        CurrentVersion = CurrentUiVersion;

        if (components.TryGetValue("ui", out var uiComponent))
        {
            LatestVersion = string.IsNullOrWhiteSpace(uiComponent.LatestVersion)
                ? LatestVersion
                : uiComponent.LatestVersion;
        }

        IsUpdateAvailable = unified.AnyUpdateAvailable;
        UpdateStatus = string.IsNullOrWhiteSpace(unified.LastError)
            ? (IsUpdateAvailable ? "Update available" : "No updates available")
            : $"Update check failed: {unified.LastError}";

        ReevaluateBannerVisibility(unifiedUpdateSucceeded: false, unifiedUpdateFailed: false);
        return true;
    }

    private static string GetInstalledComponentVersion(string executableName)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var candidates = new[]
            {
                Path.Combine(baseDir, executableName),
                Path.Combine(baseDir, "..", executableName),
                Path.Combine(baseDir, "..", "..", executableName),
                Path.Combine(programData, "StorageWatch", "Agent", executableName),
                Path.Combine(programData, "StorageWatch", "Server", executableName),
                Path.Combine(programData, "StorageWatch", "UI", executableName)
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (!File.Exists(fullPath))
                    continue;

                var version = FileVersionInfo.GetVersionInfo(fullPath).FileVersion;
                if (!string.IsNullOrWhiteSpace(version))
                    return version;
            }
        }
        catch
        {
        }

        return "0.0.0.0";
    }

    private void ReevaluateBannerVisibility(bool unifiedUpdateSucceeded, bool unifiedUpdateFailed)
    {
        if (unifiedUpdateSucceeded)
        {
            IsBannerVisible = false;
            return;
        }

        if (unifiedUpdateFailed)
        {
            IsBannerVisible = true;
            return;
        }

        IsBannerVisible = IsUpdateAvailable;
    }

    private void CancelUpdate()
    {
        _updateCts?.Cancel();
        IsUpdateInProgress = false;
        UpdateStatus = "Update cancelled";
        CloseProgressDialog();
    }

    private void DismissBanner()
    {
        IsBannerVisible = false;
    }

    private void RemindMeLater()
    {
        var intervalMinutes = Math.Max(1, _autoUpdateOptions.CurrentValue.CheckIntervalMinutes);
        _snoozeUntilUtc = DateTimeOffset.UtcNow.AddMinutes(intervalMinutes);
        IsUpdateAvailable = false;
        IsBannerVisible = false;
        UpdateStatus = "Update reminder snoozed until the next check.";
        _logger.LogInformation("[AUTOUPDATE] Remind me later selected. Snoozed for {Minutes} minutes.", intervalMinutes);
    }

    private void SkipThisVersion()
    {
        if (string.IsNullOrWhiteSpace(LatestVersion))
            return;

        _skippedVersion = LatestVersion;
        _userSettingsStore.SetSkippedVersion(_skippedVersion);

        IsUpdateAvailable = false;
        IsBannerVisible = false;
        UpdateStatus = $"Version {_skippedVersion} skipped.";
        _logger.LogInformation("[AUTOUPDATE] Skipped UI version: {Version}", _skippedVersion);
    }

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            action();
            return;
        }

        Application.Current?.Dispatcher?.Invoke(action);
    }

    private static string GetCurrentVersion()
    {
        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            return FileVersionInfo.GetVersionInfo(location).FileVersion ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }
}

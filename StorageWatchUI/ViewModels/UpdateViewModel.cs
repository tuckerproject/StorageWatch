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
        IUiUpdateDownloader updateDownloader,
        IUiUpdateInstaller updateInstaller,
        IUiAutoUpdateWorker autoUpdateWorker,
        IUiUpdateUserSettingsStore userSettingsStore,
        IOptionsMonitor<AutoUpdateOptions> autoUpdateOptions,
        ILogger<UpdateViewModel> logger)
    {
        _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
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

            var installed = await _autoUpdateWorker.TryInstallAvailableUpdateAsync(_updateCts.Token);
            if (installed)
            {
                unifiedSucceeded = true;
                _logger.LogInformation("[UPDATE] Updater handoff completed successfully");
            }
            else
            {
                unifiedFailed = true;
                if (string.IsNullOrWhiteSpace(UpdateStatus))
                    UpdateStatus = "Update failed.";
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

    private void OnUpdateCheckCompleted(object? sender, ComponentUpdateCheckResult result)
    {
        RunOnUiThread(() =>
        {
            if (result.Manifest != null)
            {
                _latestManifest = result.Manifest;
            }

            if (_snoozeUntilUtc.HasValue && DateTimeOffset.UtcNow >= _snoozeUntilUtc.Value)
            {
                _snoozeUntilUtc = null;
            }

            if (result.IsUpdateAvailable && result.Component != null)
            {
                if (!string.IsNullOrWhiteSpace(_skippedVersion) &&
                    string.Equals(result.Component.Version, _skippedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    IsUpdateAvailable = false;
                    IsBannerVisible = false;
                    UpdateStatus = "No updates available";
                    _logger.LogInformation("[AUTOUPDATE] Suppressing skipped UI version: {Version}", _skippedVersion);
                    return;
                }

                if (_snoozeUntilUtc.HasValue && DateTimeOffset.UtcNow < _snoozeUntilUtc.Value)
                {
                    IsUpdateAvailable = false;
                    IsBannerVisible = false;
                    UpdateStatus = "Update reminder snoozed.";
                    _logger.LogInformation("[AUTOUPDATE] Update notifications snoozed until {SnoozeUntilUtc}.", _snoozeUntilUtc.Value);
                    return;
                }

                IsUpdateAvailable = true;
                LatestVersion = result.Component.Version;
                ReleaseNotes = string.IsNullOrWhiteSpace(result.Component.ReleaseNotesUrl)
                    ? "Update details are available in the release notes."
                    : result.Component.ReleaseNotesUrl;
                IsBannerVisible = true;
                UpdateStatus = "Update available";
                _logger.LogInformation("Update available: {Version}", LatestVersion);
            }
            else
            {
                IsUpdateAvailable = false;
                IsBannerVisible = false;
                UpdateStatus = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "No updates available"
                    : $"Update check failed: {result.ErrorMessage}";
                if (string.IsNullOrWhiteSpace(result.ErrorMessage))
                    _logger.LogInformation("No updates available");
                else
                    _logger.LogWarning("UI update check failed: {Error}", result.ErrorMessage);
            }

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

        CurrentAgentVersion = GetInstalledComponentVersion("StorageWatchAgent.exe");
        CurrentServerVersion = GetInstalledComponentVersion("StorageWatchServer.exe");

        var uiResult = await _updateChecker.CheckForUpdateAsync(CancellationToken.None);
        if (uiResult.Manifest != null)
        {
            _latestManifest = uiResult.Manifest;
        }

        var uiNeedsUpdate = Version.TryParse(_latestManifest.Ui.Version, out var manifestUiVersion)
            && Version.TryParse(CurrentUiVersion, out var localUiVersion)
            && manifestUiVersion > localUiVersion;

        var agentNeedsUpdate = Version.TryParse(_latestManifest.Agent.Version, out var manifestAgentVersion)
            && Version.TryParse(CurrentAgentVersion, out var localAgentVersion)
            && manifestAgentVersion > localAgentVersion;

        var serverNeedsUpdate = Version.TryParse(_latestManifest.Server.Version, out var manifestServerVersion)
            && Version.TryParse(CurrentServerVersion, out var localServerVersion)
            && manifestServerVersion > localServerVersion;

        LatestVersion = _latestManifest.Ui.Version;
        IsUpdateAvailable = uiNeedsUpdate || agentNeedsUpdate || serverNeedsUpdate;
        ReevaluateBannerVisibility(unifiedUpdateSucceeded: false, unifiedUpdateFailed: false);
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

using System.Windows.Input;
using Microsoft.Extensions.Logging;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.Views;
using System.Windows;

namespace StorageWatchUI.ViewModels;

public class UpdateViewModel : ViewModelBase
{
    private bool _isUpdateAvailable;
    private string _latestVersion = string.Empty;
    private string _releaseNotes = string.Empty;
    private bool _isUpdateInProgress;
    private bool _isRestartRequired;
    private string _updateStatus = string.Empty;
    private double _updateProgress;
    private bool _isBannerVisible;
    private readonly IUiAutoUpdateWorker _autoUpdateWorker;
    private readonly IUiRestartHandler _restartHandler;
    private readonly ILogger<UpdateViewModel> _logger;
    private CancellationTokenSource? _updateCts;
    private UpdateProgressViewModel? _progressViewModel;
    private UpdateProgressDialog? _progressDialog;

    public UpdateViewModel(
        IUiUpdateChecker updateChecker,
        IUiUpdateDownloader updateDownloader,
        IUiUpdateInstaller updateInstaller,
        IUiRestartHandler restartHandler,
        IUiAutoUpdateWorker autoUpdateWorker,
        ILogger<UpdateViewModel> logger)
    {
        _autoUpdateWorker = autoUpdateWorker ?? throw new ArgumentNullException(nameof(autoUpdateWorker));
        _restartHandler = restartHandler ?? throw new ArgumentNullException(nameof(restartHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _autoUpdateWorker.UpdateCheckCompleted += OnUpdateCheckCompleted;
        _autoUpdateWorker.UpdateProgressChanged += OnUpdateProgressChanged;
        _autoUpdateWorker.UpdateInstallCompleted += OnUpdateInstallCompleted;
        _autoUpdateWorker.RestartPromptRequested += OnRestartPromptRequested;

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

        var dialogVm = new UpdateDialogViewModel
        {
            CurrentVersion = GetCurrentVersion(),
            NewVersion = LatestVersion,
            ReleaseNotes = ReleaseNotes
        };

        var dialog = new UpdateDialog(dialogVm);

        dialogVm.UpdateRequested += async (s, e) =>
        {
            dialog.Close();
            await PerformUpdateAsync();
        };

        dialogVm.CancelRequested += (s, e) => dialog.Close();

        dialog.ShowDialog();
    }

    private async Task PerformUpdateAsync()
    {
        if (_autoUpdateWorker.IsCycleActive)
        {
            _logger.LogInformation("[AUTOUPDATE] Skipping manual install — background update cycle is already active.");
            UpdateStatus = "Update already in progress.";
            return;
        }

        try
        {
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

            var started = await _autoUpdateWorker.TryInstallAvailableUpdateAsync(_updateCts.Token);
            if (!started)
            {
                IsUpdateInProgress = false;
                CloseProgressDialog();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update cancelled");
            UpdateStatus = "Update cancelled";
            IsUpdateInProgress = false;
            CloseProgressDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update");
            UpdateStatus = $"Error: {ex.Message}";
            IsUpdateInProgress = false;
            CloseProgressDialog();
            MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnUpdateCheckCompleted(object? sender, ComponentUpdateCheckResult result)
    {
        RunOnUiThread(() =>
        {
            if (result.IsUpdateAvailable && result.Component != null)
            {
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
        });
    }

    private void OnUpdateProgressChanged(object? sender, UiUpdateProgressInfo progress)
    {
        RunOnUiThread(() =>
        {
            IsUpdateInProgress = true;
            UpdateStatus = progress.Status;
            UpdateProgress = progress.ProgressPercent;

            if (_progressViewModel != null)
            {
                _progressViewModel.StatusText = progress.Status;
                _progressViewModel.Progress = progress.ProgressPercent;
                _progressViewModel.IsIndeterminate = progress.IsIndeterminate;
            }
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
                UpdateStatus = "Update installed successfully";
                UpdateProgress = 100;
            }
            else
            {
                UpdateStatus = $"Update failed: {result.ErrorMessage}";
                MessageBox.Show($"Update failed: {result.ErrorMessage}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            CloseProgressDialog();
        });
    }

    private void OnRestartPromptRequested(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            IsRestartRequired = true;
            ShowRestartPrompt();
        });
    }

    private void ShowRestartPrompt()
    {
        var restartDialog = new RestartPromptDialog();
        if (restartDialog.ShowDialog() == true)
        {
            RestartNow();
        }
    }

    private void CloseProgressDialog()
    {
        if (_progressDialog == null)
            return;

        _progressDialog.Close();
        _progressDialog = null;
        _progressViewModel = null;
    }

    private void CancelUpdate()
    {
        _updateCts?.Cancel();
        IsUpdateInProgress = false;
        UpdateStatus = "Update cancelled";
        CloseProgressDialog();
    }

    private void RestartNow()
    {
        try
        {
            _restartHandler.RequestRestart();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting application");
            UpdateStatus = $"Error restarting: {ex.Message}";
        }
    }

    private void DismissBanner()
    {
        IsBannerVisible = false;
    }

    private void RemindMeLater()
    {
        IsBannerVisible = false;
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
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }
}

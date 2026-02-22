using System.Windows.Input;
using Microsoft.Extensions.Logging;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.Views;
using System.Windows;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// Main ViewModel for managing update state and user interactions.
/// </summary>
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
    private readonly IUiUpdateChecker _updateChecker;
    private readonly IUiUpdateDownloader _updateDownloader;
    private readonly IUiUpdateInstaller _updateInstaller;
    private readonly IUiRestartHandler _restartHandler;
    private readonly ILogger<UpdateViewModel> _logger;
    private CancellationTokenSource? _updateCts;

    public UpdateViewModel(
        IUiUpdateChecker updateChecker,
        IUiUpdateDownloader updateDownloader,
        IUiUpdateInstaller updateInstaller,
        IUiRestartHandler restartHandler,
        ILogger<UpdateViewModel> logger)
    {
        _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
        _updateDownloader = updateDownloader ?? throw new ArgumentNullException(nameof(updateDownloader));
        _updateInstaller = updateInstaller ?? throw new ArgumentNullException(nameof(updateInstaller));
        _restartHandler = restartHandler ?? throw new ArgumentNullException(nameof(restartHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        try
        {
            UpdateStatus = "Checking for updates...";
            _updateCts = new CancellationTokenSource();
            
            var result = await _updateChecker.CheckForUpdateAsync(_updateCts.Token);
            
            if (result.IsUpdateAvailable && result.Component != null)
            {
                IsUpdateAvailable = true;
                LatestVersion = result.Component.Version;
                IsBannerVisible = true;
                _logger.LogInformation($"Update available: {LatestVersion}");
            }
            else
            {
                IsUpdateAvailable = false;
                IsBannerVisible = false;
                UpdateStatus = "No updates available";
                _logger.LogInformation("No updates available");
            }
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

        // Show update dialog
        var dialogVm = new UpdateDialogViewModel();
        var currentVersion = GetCurrentVersion();
        dialogVm.CurrentVersion = currentVersion;
        dialogVm.NewVersion = LatestVersion;
        dialogVm.ReleaseNotes = ReleaseNotes;

        var dialog = new UpdateDialog(dialogVm);
        
        // Handle dialog result
        dialogVm.UpdateRequested += async (s, e) =>
        {
            dialog.Close();
            await PerformUpdateAsync();
        };

        dialogVm.CancelRequested += (s, e) =>
        {
            dialog.Close();
        };

        dialog.ShowDialog();
    }

    private async Task PerformUpdateAsync()
    {
        try
        {
            IsUpdateInProgress = true;
            UpdateProgress = 0;
            _updateCts = new CancellationTokenSource();

            // Show progress dialog
            var progressVm = new UpdateProgressViewModel();
            var progressDialog = new UpdateProgressDialog(progressVm);
            
            progressVm.CancelRequested += (s, e) =>
            {
                CancelUpdate();
                progressDialog.Close();
            };

            // Check again to get fresh component info
            var checkResult = await _updateChecker.CheckForUpdateAsync(_updateCts.Token);
            if (checkResult.Component == null)
                throw new InvalidOperationException("Update component information unavailable");

            // Download
            progressVm.StatusText = "Downloading update...";
            progressVm.IsIndeterminate = true;
            progressDialog.Show();

            var downloadResult = await _updateDownloader.DownloadAsync(
                checkResult.Component,
                _updateCts.Token);

            if (!downloadResult.Success || string.IsNullOrWhiteSpace(downloadResult.FilePath))
                throw new InvalidOperationException(downloadResult.ErrorMessage ?? "Download failed");

            // Verify
            progressVm.StatusText = "Verifying integrity...";
            progressVm.IsIndeterminate = true;
            progressVm.Progress = 50;

            // Install
            progressVm.StatusText = "Installing update...";
            progressVm.Progress = 75;
            
            var installResult = await _updateInstaller.InstallAsync(
                downloadResult.FilePath,
                _updateCts.Token);

            if (!installResult.Success)
                throw new InvalidOperationException(installResult.ErrorMessage ?? "Installation failed");

            progressVm.StatusText = "Update installed successfully";
            progressVm.Progress = 100;
            progressDialog.Close();

            IsRestartRequired = true;
            ShowRestartPrompt();
            
            _logger.LogInformation("Update installed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update cancelled");
            UpdateStatus = "Update cancelled";
            IsUpdateInProgress = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update");
            UpdateStatus = $"Error: {ex.Message}";
            IsUpdateInProgress = false;
            MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowRestartPrompt()
    {
        var restartDialog = new RestartPromptDialog();
        if (restartDialog.ShowDialog() == true)
        {
            RestartNow();
        }
    }

    private void CancelUpdate()
    {
        _updateCts?.Cancel();
        IsUpdateInProgress = false;
        UpdateStatus = "Update cancelled";
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

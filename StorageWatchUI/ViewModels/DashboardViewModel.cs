using StorageWatchUI.Models;
using StorageWatchUI.Services;
using StorageWatchUI.Services.Logging;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for the Dashboard view showing current disk status.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private readonly IDataProvider _dataProvider;
    private readonly ConfigurationService _configService;
    private readonly RollingFileLogger? _logger;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private System.Timers.Timer? _refreshTimer;

    public DashboardViewModel(IDataProvider dataProvider, ConfigurationService configService, RollingFileLogger? logger = null)
    {
        _dataProvider = dataProvider;
        _configService = configService;
        _logger = logger;

        _logger?.Log("[VIEWMODEL] Loading DashboardViewModel...");

        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());

        // Start auto-refresh timer (every 30 seconds)
        _refreshTimer = new System.Timers.Timer(30000);
        _refreshTimer.Elapsed += async (s, e) => await LoadDataAsync();
        _refreshTimer.Start();

        _logger?.Log("[VIEWMODEL] DashboardViewModel initialized");
    }

    public ObservableCollection<DiskInfo> Disks { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading disk information...";

        try
        {
            _logger?.Log("[DB] Querying disk data from Agent database");
            var disks = await _dataProvider.GetCurrentDiskStatusAsync();

            _logger?.Log($"[VIEWMODEL] Bound {disks.Count} drives to UI");

            // Dispatch collection modifications to the UI thread
            if (Application.Current?.Dispatcher != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Disks.Clear();
                    foreach (var disk in disks)
                    {
                        Disks.Add(disk);
                    }
                });
            }
            else
            {
                // In unit tests or headless scenarios, update directly
                Disks.Clear();
                foreach (var disk in disks)
                {
                    Disks.Add(disk);
                }
            }

            StatusMessage = disks.Any()
                ? $"Last updated: {DateTime.Now:HH:mm:ss}"
                : "No disk data available. Ensure StorageWatch service is running.";
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ERROR] ViewModel load failed: {ex.Message}");
            StatusMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

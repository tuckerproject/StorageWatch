using StorageWatchUI.Models;
using StorageWatchUI.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for the Central view showing machines reporting to central server.
/// </summary>
public class CentralViewModel : ViewModelBase
{
    private readonly CentralDataProvider _centralDataProvider;
    private readonly ConfigurationService _configService;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _isCentralEnabled;
    private System.Timers.Timer? _refreshTimer;

    public CentralViewModel(CentralDataProvider centralDataProvider, ConfigurationService configService)
    {
        _centralDataProvider = centralDataProvider;
        _configService = configService;

        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        
        _isCentralEnabled = _centralDataProvider.IsEnabled;

        if (_isCentralEnabled)
        {
            // Start auto-refresh timer (every 60 seconds)
            _refreshTimer = new System.Timers.Timer(60000);
            _refreshTimer.Elapsed += async (s, e) => await LoadDataAsync();
            _refreshTimer.Start();
        }
    }

    public ObservableCollection<MachineStatus> Machines { get; } = new();

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

    public bool IsCentralEnabled
    {
        get => _isCentralEnabled;
        set => SetProperty(ref _isCentralEnabled, value);
    }

    public ICommand RefreshCommand { get; }

    private async Task LoadDataAsync()
    {
        if (!IsCentralEnabled)
        {
            StatusMessage = "Central server is not enabled in configuration.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Connecting to central server...";

        try
        {
            var isHealthy = await _centralDataProvider.CheckHealthAsync();
            if (!isHealthy)
            {
                StatusMessage = "Cannot connect to central server. Check server URL and network.";
                return;
            }

            var machines = await _centralDataProvider.GetAllMachineStatusAsync();
            
            Machines.Clear();
            foreach (var machine in machines)
            {
                Machines.Add(machine);
            }

            StatusMessage = machines.Any() 
                ? $"Showing {machines.Count} machine(s). Last updated: {DateTime.Now:HH:mm:ss}" 
                : "No machines are reporting to the central server.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

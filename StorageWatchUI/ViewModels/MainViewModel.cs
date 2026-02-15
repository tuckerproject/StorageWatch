using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// Main ViewModel that manages navigation between different views.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private ViewModelBase? _currentViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly TrendsViewModel _trendsViewModel;
    private readonly CentralViewModel _centralViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ServiceStatusViewModel _serviceStatusViewModel;

    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        TrendsViewModel trendsViewModel,
        CentralViewModel centralViewModel,
        SettingsViewModel settingsViewModel,
        ServiceStatusViewModel serviceStatusViewModel)
    {
        _dashboardViewModel = dashboardViewModel;
        _trendsViewModel = trendsViewModel;
        _centralViewModel = centralViewModel;
        _settingsViewModel = settingsViewModel;
        _serviceStatusViewModel = serviceStatusViewModel;

        // Start with Dashboard view
        CurrentViewModel = _dashboardViewModel;

        // Setup commands
        NavigateToDashboardCommand = new RelayCommand(NavigateToDashboard);
        NavigateToTrendsCommand = new RelayCommand(NavigateToTrends);
        NavigateToCentralCommand = new RelayCommand(NavigateToCentral);
        NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);
        NavigateToServiceStatusCommand = new RelayCommand(NavigateToServiceStatus);
    }

    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public ICommand NavigateToDashboardCommand { get; }
    public ICommand NavigateToTrendsCommand { get; }
    public ICommand NavigateToCentralCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }
    public ICommand NavigateToServiceStatusCommand { get; }

    private void NavigateToDashboard()
    {
        CurrentViewModel = _dashboardViewModel;
        _dashboardViewModel.RefreshCommand.Execute(null);
    }

    private void NavigateToTrends()
    {
        CurrentViewModel = _trendsViewModel;
        _trendsViewModel.RefreshCommand.Execute(null);
    }

    private void NavigateToCentral()
    {
        CurrentViewModel = _centralViewModel;
        _centralViewModel.RefreshCommand.Execute(null);
    }

    private void NavigateToSettings()
    {
        CurrentViewModel = _settingsViewModel;
        _settingsViewModel.RefreshCommand.Execute(null);
    }

    private void NavigateToServiceStatus()
    {
        CurrentViewModel = _serviceStatusViewModel;
        _serviceStatusViewModel.RefreshCommand.Execute(null);
    }
}

/// <summary>
/// Simple RelayCommand implementation for commands.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

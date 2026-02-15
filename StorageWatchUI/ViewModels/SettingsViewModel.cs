using StorageWatchUI.Services;
using System.Windows;
using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for the Settings view.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigurationService _configService;
    private string _configurationJson = string.Empty;
    private bool _isLoading;

    public SettingsViewModel(ConfigurationService configService)
    {
        _configService = configService;

        RefreshCommand = new RelayCommand(async () => await LoadConfigurationAsync());
        OpenConfigCommand = new RelayCommand(OpenConfiguration);
        TestAlertsCommand = new RelayCommand(TestAlerts);
    }

    public string ConfigurationJson
    {
        get => _configurationJson;
        set => SetProperty(ref _configurationJson, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenConfigCommand { get; }
    public ICommand TestAlertsCommand { get; }

    private async Task LoadConfigurationAsync()
    {
        IsLoading = true;

        try
        {
            ConfigurationJson = await _configService.GetConfigurationAsJsonAsync();
        }
        catch (Exception ex)
        {
            ConfigurationJson = $"Error loading configuration: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenConfiguration()
    {
        try
        {
            _configService.OpenConfigInNotepad();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot open configuration file: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TestAlerts()
    {
        MessageBox.Show(
            "Alert testing functionality will be implemented in a future update.\n\n" +
            "This will allow you to send test alerts through all configured alert senders " +
            "(SMTP, GroupMe, etc.) to verify they are working correctly.",
            "Test Alerts", 
            MessageBoxButton.OK, 
            MessageBoxImage.Information);
    }
}

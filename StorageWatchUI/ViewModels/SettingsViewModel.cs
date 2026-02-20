using StorageWatchUI.Services;
using StorageWatchUI.Communication;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for the Settings view.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigurationService _configService;
    private readonly ServiceCommunicationClient _communicationClient;
    private string _configurationJson = string.Empty;
    private bool _isLoading;
    private string _validationStatus = "Unknown";
    private bool _isConfigValid = true;

    public SettingsViewModel(ConfigurationService configService)
    {
        _configService = configService;
        _communicationClient = new ServiceCommunicationClient();

        RefreshCommand = new RelayCommand(async () => await LoadConfigurationAsync());
        OpenConfigCommand = new RelayCommand(OpenConfiguration);
        TestAlertsCommand = new RelayCommand(async () => await TestAlertsAsync());
        ValidateConfigCommand = new RelayCommand(async () => await ValidateConfigurationAsync());
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

    public string ValidationStatus
    {
        get => _validationStatus;
        set => SetProperty(ref _validationStatus, value);
    }

    public bool IsConfigValid
    {
        get => _isConfigValid;
        set => SetProperty(ref _isConfigValid, value);
    }

    public ObservableCollection<string> ValidationErrors { get; } = new();
    public ObservableCollection<string> ValidationWarnings { get; } = new();
    public ObservableCollection<PluginStatusInfo> PluginStatuses { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand OpenConfigCommand { get; }
    public ICommand TestAlertsCommand { get; }
    public ICommand ValidateConfigCommand { get; }

    private async Task LoadConfigurationAsync()
    {
        IsLoading = true;

        try
        {
            // Try to get config via IPC first
            var configData = await _communicationClient.GetConfigAsync();
            if (configData.HasValue)
            {
                ConfigurationJson = configData.Value.Content;
            }
            else
            {
                // Fallback to local reading
                ConfigurationJson = await _configService.GetConfigurationAsJsonAsync();
            }

            // Load plugin status
            await LoadPluginStatusAsync();

            // Validate configuration
            await ValidateConfigurationAsync();
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

    private async Task ValidateConfigurationAsync()
    {
        try
        {
            var validation = await _communicationClient.ValidateConfigAsync();

            ValidationErrors.Clear();
            ValidationWarnings.Clear();

            if (validation != null)
            {
                IsConfigValid = validation.IsValid;

                foreach (var error in validation.Errors)
                {
                    ValidationErrors.Add(error);
                }

                foreach (var warning in validation.Warnings)
                {
                    ValidationWarnings.Add(warning);
                }

                if (validation.IsValid && validation.Warnings.Count == 0)
                {
                    ValidationStatus = "✓ Configuration is valid";
                }
                else if (validation.IsValid && validation.Warnings.Count > 0)
                {
                    ValidationStatus = $"⚠ Configuration valid with {validation.Warnings.Count} warning(s)";
                }
                else
                {
                    ValidationStatus = $"✗ Configuration has {validation.Errors.Count} error(s)";
                }
            }
            else
            {
                ValidationStatus = "⚠ Unable to validate (service not responding)";
            }
        }
        catch (Exception ex)
        {
            ValidationStatus = $"✗ Validation error: {ex.Message}";
        }
    }

    private async Task LoadPluginStatusAsync()
    {
        try
        {
            var plugins = await _communicationClient.GetPluginStatusAsync();

            PluginStatuses.Clear();
            if (plugins != null)
            {
                foreach (var plugin in plugins)
                {
                    PluginStatuses.Add(plugin);
                }
            }
        }
        catch
        {
            // Ignore errors - plugin status is optional
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

    private async Task TestAlertsAsync()
    {
        IsLoading = true;

        try
        {
            var response = await _communicationClient.TestAlertSendersAsync();

            if (response.Success)
            {
                MessageBox.Show(
                    "Test alerts sent successfully!\n\n" +
                    "Check your configured alert destinations (email, GroupMe, etc.) to verify receipt.",
                    "Test Alerts",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Failed to send test alerts:\n{response.ErrorMessage}",
                    "Test Alerts",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error sending test alerts: {ex.Message}\n\n" +
                "Ensure the StorageWatch service is running.",
                "Test Alerts",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

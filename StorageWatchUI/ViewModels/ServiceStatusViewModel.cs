using StorageWatchUI.Models;
using StorageWatchUI.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for the Service Status view.
/// </summary>
public class ServiceStatusViewModel : ViewModelBase
{
    private readonly ServiceManager _serviceManager;
    private bool _isServiceInstalled;
    private string _serviceStatus = "Unknown";
    private bool _isLoading;
    private bool _canStart;
    private bool _canStop;
    private System.Timers.Timer? _refreshTimer;

    public ServiceStatusViewModel(ServiceManager serviceManager)
    {
        _serviceManager = serviceManager;

        RefreshCommand = new RelayCommand(async () => await LoadStatusAsync());
        StartServiceCommand = new RelayCommand(async () => await StartServiceAsync(), () => CanStart);
        StopServiceCommand = new RelayCommand(async () => await StopServiceAsync(), () => CanStop);
        RestartServiceCommand = new RelayCommand(async () => await RestartServiceAsync(), () => CanStop);
        RefreshLogsCommand = new RelayCommand(LoadLogs);

        // Start auto-refresh timer (every 10 seconds)
        _refreshTimer = new System.Timers.Timer(10000);
        _refreshTimer.Elapsed += async (s, e) => await LoadStatusAsync();
        _refreshTimer.Start();
    }

    public ObservableCollection<LogEntry> RecentLogs { get; } = new();

    public bool IsServiceInstalled
    {
        get => _isServiceInstalled;
        set => SetProperty(ref _isServiceInstalled, value);
    }

    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetProperty(ref _serviceStatus, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool CanStart
    {
        get => _canStart;
        set
        {
            if (SetProperty(ref _canStart, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanStop
    {
        get => _canStop;
        set
        {
            if (SetProperty(ref _canStop, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand StartServiceCommand { get; }
    public ICommand StopServiceCommand { get; }
    public ICommand RestartServiceCommand { get; }
    public ICommand RefreshLogsCommand { get; }

    private async Task LoadStatusAsync()
    {
        await Task.Run(() =>
        {
            IsServiceInstalled = _serviceManager.IsServiceInstalled();

            if (!IsServiceInstalled)
            {
                ServiceStatus = "Not Installed";
                CanStart = false;
                CanStop = false;
                return;
            }

            var status = _serviceManager.GetServiceStatus();
            ServiceStatus = status.ToString();

            CanStart = status == ServiceControllerStatus.Stopped;
            CanStop = status == ServiceControllerStatus.Running;
        });

        LoadLogs();
    }

    private async Task StartServiceAsync()
    {
        IsLoading = true;

        try
        {
            var success = await _serviceManager.StartServiceAsync();
            if (success)
            {
                MessageBox.Show("Service started successfully.", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to start service. Check permissions and try running as administrator.", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting service: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            await LoadStatusAsync();
        }
    }

    private async Task StopServiceAsync()
    {
        IsLoading = true;

        try
        {
            var success = await _serviceManager.StopServiceAsync();
            if (success)
            {
                MessageBox.Show("Service stopped successfully.", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to stop service. Check permissions and try running as administrator.", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error stopping service: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            await LoadStatusAsync();
        }
    }

    private async Task RestartServiceAsync()
    {
        IsLoading = true;

        try
        {
            var success = await _serviceManager.RestartServiceAsync();
            if (success)
            {
                MessageBox.Show("Service restarted successfully.", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to restart service. Check permissions and try running as administrator.", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error restarting service: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            await LoadStatusAsync();
        }
    }

    private void LoadLogs()
    {
        try
        {
            var logPath = GetLogFilePath();
            if (!File.Exists(logPath))
            {
                RecentLogs.Clear();
                RecentLogs.Add(new LogEntry 
                { 
                    Timestamp = DateTime.Now, 
                    Message = "Log file not found.", 
                    Level = LogLevel.Warning 
                });
                return;
            }

            var lines = File.ReadLines(logPath).Reverse().Take(20).Reverse().ToList();
            
            RecentLogs.Clear();
            foreach (var line in lines)
            {
                var entry = ParseLogLine(line);
                RecentLogs.Add(entry);
            }
        }
        catch (Exception ex)
        {
            RecentLogs.Clear();
            RecentLogs.Add(new LogEntry 
            { 
                Timestamp = DateTime.Now, 
                Message = $"Error reading logs: {ex.Message}", 
                Level = LogLevel.Error 
            });
        }
    }

    private string GetLogFilePath()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "StorageWatch", "Logs", "service.log");
    }

    private LogEntry ParseLogLine(string line)
    {
        try
        {
            // Expected format: "2024-01-15 10:30:45  Message text"
            if (line.Length < 21)
                return new LogEntry { Timestamp = DateTime.Now, Message = line, Level = LogLevel.Info };

            var timestampText = line.Substring(0, 19);
            var message = line.Substring(21);

            if (DateTime.TryParse(timestampText, out var timestamp))
            {
                var level = message.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ? LogLevel.Error :
                            message.Contains("WARNING", StringComparison.OrdinalIgnoreCase) ? LogLevel.Warning :
                            LogLevel.Info;

                return new LogEntry { Timestamp = timestamp, Message = message, Level = level };
            }
        }
        catch { }

        return new LogEntry { Timestamp = DateTime.Now, Message = line, Level = LogLevel.Info };
    }
}

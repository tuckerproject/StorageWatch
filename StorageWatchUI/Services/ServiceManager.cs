using System.Diagnostics;
using System.ServiceProcess;

namespace StorageWatchUI.Services;

/// <summary>
/// Manages the StorageWatch Windows Service (start, stop, restart, status).
/// </summary>
public class ServiceManager
{
    private const string ServiceName = "StorageWatchService";

    /// <summary>
    /// Checks if the StorageWatch service is installed.
    /// </summary>
    public bool IsServiceInstalled()
    {
        try
        {
            var services = ServiceController.GetServices();
            return services.Any(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the StorageWatch service is running.
    /// </summary>
    public ServiceControllerStatus GetServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            return sc.Status;
        }
        catch
        {
            return ServiceControllerStatus.Stopped;
        }
    }

    /// <summary>
    /// Starts the StorageWatch service.
    /// </summary>
    public async Task<bool> StartServiceAsync()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status == ServiceControllerStatus.Running)
                return true;

            sc.Start();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting service: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Stops the StorageWatch service.
    /// </summary>
    public async Task<bool> StopServiceAsync()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
                return true;

            sc.Stop();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping service: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Restarts the StorageWatch service.
    /// </summary>
    public async Task<bool> RestartServiceAsync()
    {
        var stopped = await StopServiceAsync();
        if (!stopped)
            return false;

        await Task.Delay(2000); // Wait a bit before starting
        return await StartServiceAsync();
    }
}

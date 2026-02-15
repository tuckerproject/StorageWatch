using System.Diagnostics;
using System.ServiceProcess;
using System.Security.Principal;

namespace StorageWatchUI.Services;

/// <summary>
/// Manages the StorageWatch Windows Service (start, stop, restart, status).
/// Includes elevation support for operations requiring administrator privileges.
/// </summary>
public class ServiceManager : IServiceManager
{
    private const string ServiceName = "StorageWatchService";

    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    public bool IsRunningAsAdmin()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

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
    /// If not running as admin, will attempt to elevate.
    /// </summary>
    public async Task<bool> StartServiceAsync()
    {
        if (!IsRunningAsAdmin())
        {
            return RunElevated("start");
        }

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
    /// If not running as admin, will attempt to elevate.
    /// </summary>
    public async Task<bool> StopServiceAsync()
    {
        if (!IsRunningAsAdmin())
        {
            return RunElevated("stop");
        }

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

    /// <summary>
    /// Runs a service control command with elevation (UAC prompt).
    /// </summary>
    private bool RunElevated(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"{command} {ServiceName}",
                Verb = "runas", // Request elevation
                UseShellExecute = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit(30000); // Wait up to 30 seconds
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC prompt
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error running elevated command: {ex}");
            return false;
        }
    }
}

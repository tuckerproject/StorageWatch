using System.ServiceProcess;

namespace StorageWatchUI.Services;

/// <summary>
/// Interface for managing the StorageWatch Windows Service.
/// Enables testability through dependency injection and mocking.
/// </summary>
public interface IServiceManager
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    bool IsRunningAsAdmin();

    /// <summary>
    /// Checks if the StorageWatch service is installed.
    /// </summary>
    bool IsServiceInstalled();

    /// <summary>
    /// Gets the current status of the StorageWatch service.
    /// </summary>
    ServiceControllerStatus GetServiceStatus();

    /// <summary>
    /// Starts the StorageWatch service.
    /// If not running as admin, will attempt to elevate.
    /// </summary>
    Task<bool> StartServiceAsync();

    /// <summary>
    /// Stops the StorageWatch service.
    /// If not running as admin, will attempt to elevate.
    /// </summary>
    Task<bool> StopServiceAsync();

    /// <summary>
    /// Restarts the StorageWatch service.
    /// </summary>
    Task<bool> RestartServiceAsync();
}

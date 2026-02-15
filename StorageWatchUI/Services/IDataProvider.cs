using StorageWatchUI.Models;

namespace StorageWatchUI.Services;

/// <summary>
/// Interface for data providers that fetch storage monitoring data.
/// </summary>
public interface IDataProvider
{
    /// <summary>
    /// Gets current disk information for the local machine.
    /// </summary>
    Task<List<DiskInfo>> GetCurrentDiskStatusAsync();

    /// <summary>
    /// Gets historical trend data for a specific drive.
    /// </summary>
    Task<List<TrendDataPoint>> GetTrendDataAsync(string driveName, int daysBack = 7);

    /// <summary>
    /// Gets all available drives that have been monitored.
    /// </summary>
    Task<List<string>> GetMonitoredDrivesAsync();
}

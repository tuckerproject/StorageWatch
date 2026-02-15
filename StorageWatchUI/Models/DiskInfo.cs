namespace StorageWatchUI.Models;

/// <summary>
/// Represents current disk information for display in the UI.
/// </summary>
public class DiskInfo
{
    public string DriveName { get; set; } = string.Empty;
    public double TotalSpaceGb { get; set; }
    public double FreeSpaceGb { get; set; }
    public double UsedSpaceGb => TotalSpaceGb - FreeSpaceGb;
    public double PercentFree => TotalSpaceGb > 0 ? Math.Round((FreeSpaceGb / TotalSpaceGb) * 100, 2) : 0;
    public double PercentUsed => 100 - PercentFree;
    public DiskStatusLevel Status { get; set; }
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Returns the status based on free space percentage and threshold.
    /// </summary>
    public static DiskStatusLevel CalculateStatus(double percentFree, double thresholdPercent = 10)
    {
        if (percentFree <= thresholdPercent)
            return DiskStatusLevel.Critical;
        if (percentFree <= thresholdPercent * 2)
            return DiskStatusLevel.Warning;
        return DiskStatusLevel.OK;
    }
}

/// <summary>
/// Represents the status level of a disk.
/// </summary>
public enum DiskStatusLevel
{
    OK,
    Warning,
    Critical
}

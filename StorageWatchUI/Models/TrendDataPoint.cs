namespace StorageWatchUI.Models;

/// <summary>
/// Represents a data point for trend charts.
/// </summary>
public class TrendDataPoint
{
    public DateTime Timestamp { get; set; }
    public string DriveName { get; set; } = string.Empty;
    public double PercentFree { get; set; }
    public double FreeSpaceGb { get; set; }
    public double TotalSpaceGb { get; set; }
}

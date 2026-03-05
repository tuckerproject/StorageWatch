namespace StorageWatchServer.Server.Models;

/// <summary>
/// Represents a raw drive row exactly as received from the Agent.
/// No transformations or aggregations applied.
/// </summary>
public class RawDriveRow
{
    public long Id { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string DriveLetter { get; set; } = string.Empty;

    public double TotalSpaceGb { get; set; }

    public double UsedSpaceGb { get; set; }

    public double FreeSpaceGb { get; set; }

    public double PercentFree { get; set; }

    public DateTime Timestamp { get; set; }
}

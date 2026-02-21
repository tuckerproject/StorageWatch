namespace StorageWatchServer.Server.Reporting.Models;

public class DriveReport
{
    public string DriveLetter { get; set; } = string.Empty;

    public double TotalSpaceGb { get; set; }

    public double FreeSpaceGb { get; set; }

    public double UsedPercent { get; set; }

    public DateTime TimestampUtc { get; set; }
}

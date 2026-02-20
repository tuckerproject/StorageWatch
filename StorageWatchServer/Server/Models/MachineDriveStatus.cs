namespace StorageWatchServer.Server.Models;

public class MachineDriveStatus
{
    public required string DriveLetter { get; init; }

    public double TotalSpaceGb { get; init; }

    public double UsedSpaceGb { get; init; }

    public double FreeSpaceGb { get; init; }

    public double PercentFree { get; init; }

    public DateTime LastSeenUtc { get; init; }
}

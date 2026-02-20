namespace StorageWatchServer.Server.Models;

public class DiskHistoryPoint
{
    public DateTime CollectionTimeUtc { get; init; }

    public double TotalSpaceGb { get; init; }

    public double UsedSpaceGb { get; init; }

    public double FreeSpaceGb { get; init; }

    public double PercentFree { get; init; }
}

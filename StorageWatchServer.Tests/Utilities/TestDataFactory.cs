using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Models;

namespace StorageWatchServer.Tests.Utilities;

/// <summary>
/// Factory for creating test data objects.
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Creates a test AgentReportRequest with the new consolidated format.
    /// </summary>
    public static AgentReportRequest CreateAgentReport(
        string machineName = "TestMachine",
        int rowCount = 2)
    {
        var report = new AgentReportRequest
        {
            MachineName = machineName,
            Rows = new List<RawDriveRowRequest>()
        };

        var driveLetter = 'C';
        for (int i = 0; i < rowCount; i++)
        {
            report.Rows.Add(new RawDriveRowRequest
            {
                DriveLetter = $"{driveLetter}:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 250,
                FreeSpaceGb = 250,
                PercentFree = 50,
                Timestamp = DateTime.UtcNow
            });
            driveLetter++;
        }

        return report;
    }

    public static DiskHistoryPoint CreateHistoryPoint(
        DateTime? collectionTimeUtc = null,
        double percentFree = 50)
    {
        return new DiskHistoryPoint
        {
            CollectionTimeUtc = collectionTimeUtc ?? DateTime.UtcNow,
            TotalSpaceGb = 500,
            UsedSpaceGb = 250,
            FreeSpaceGb = 250,
            PercentFree = percentFree
        };
    }

    public static RawDriveRow CreateRawDriveRow(
        string machineName = "TestMachine",
        string driveLetter = "C:",
        double totalSpaceGb = 500,
        double usedSpaceGb = 250,
        double freeSpaceGb = 250,
        double percentFree = 50,
        DateTime? timestamp = null)
    {
        return new RawDriveRow
        {
            MachineName = machineName,
            DriveLetter = driveLetter,
            TotalSpaceGb = totalSpaceGb,
            UsedSpaceGb = usedSpaceGb,
            FreeSpaceGb = freeSpaceGb,
            PercentFree = percentFree,
            Timestamp = timestamp ?? DateTime.UtcNow
        };
    }

    public static MachineDriveStatus CreateDriveStatus(
        string driveLetter = "C:",
        double percentFree = 50,
        DateTime? lastSeenUtc = null)
    {
        return new MachineDriveStatus
        {
            DriveLetter = driveLetter,
            TotalSpaceGb = 500,
            UsedSpaceGb = 250,
            FreeSpaceGb = 250,
            PercentFree = percentFree,
            LastSeenUtc = lastSeenUtc ?? DateTime.UtcNow
        };
    }
}

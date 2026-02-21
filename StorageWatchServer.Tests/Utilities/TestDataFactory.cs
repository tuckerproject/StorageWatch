using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Models;

namespace StorageWatchServer.Tests.Utilities;

/// <summary>
/// Factory for creating test data objects.
/// </summary>
public static class TestDataFactory
{
    public static AgentReportRequest CreateAgentReport(
        string machineName = "TestMachine",
        DateTime? collectionTimeUtc = null,
        int driveCount = 2)
    {
        var report = new AgentReportRequest
        {
            MachineName = machineName,
            CollectionTimeUtc = collectionTimeUtc ?? DateTime.UtcNow,
            Drives = new List<AgentDriveReport>()
        };

        var driveLetter = 'C';
        for (int i = 0; i < driveCount; i++)
        {
            report.Drives.Add(new AgentDriveReport
            {
                DriveLetter = $"{driveLetter}:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 350,
                FreeSpaceGb = 150,
                PercentFree = 30,
                CollectionTimeUtc = collectionTimeUtc ?? DateTime.UtcNow
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

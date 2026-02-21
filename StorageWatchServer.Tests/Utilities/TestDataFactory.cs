using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Models;

namespace StorageWatchServer.Tests.Utilities;

/// <summary>
/// Factory for creating test data objects.
/// </summary>
public static class TestDataFactory
{
    public static AgentReportRequest CreateAgentReport(
        string agentId = "TestAgent",
        DateTime? timestampUtc = null,
        int driveCount = 2,
        int alertCount = 1)
    {
        var report = new AgentReportRequest
        {
            AgentId = agentId,
            TimestampUtc = timestampUtc ?? DateTime.UtcNow,
            Drives = new List<DriveReportDto>(),
            Alerts = new List<AlertDto>()
        };

        var driveLetter = 'C';
        for (int i = 0; i < driveCount; i++)
        {
            report.Drives.Add(new DriveReportDto
            {
                DriveLetter = $"{driveLetter}:",
                TotalSpaceGb = 500,
                FreeSpaceGb = 150,
                UsedPercent = 70
            });
            driveLetter++;
        }

        for (int i = 0; i < alertCount; i++)
        {
            report.Alerts.Add(new AlertDto
            {
                DriveLetter = "C:",
                Level = "Warning",
                Message = "Low disk space"
            });
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

using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Reporting;
using Xunit;

namespace StorageWatchServer.Tests.Reporting;

public class AgentReportMapperTests
{
    [Fact]
    public void Map_UsesRequestTimestampWhenProvided()
    {
        var timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var request = new AgentReportRequest
        {
            AgentId = "agent-1",
            TimestampUtc = timestamp,
            Drives =
            {
                new DriveReportDto
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    FreeSpaceGb = 200,
                    UsedPercent = 60
                }
            },
            Alerts =
            {
                new AlertDto
                {
                    DriveLetter = "C:",
                    Level = "Warning",
                    Message = "Low space"
                }
            }
        };

        var report = AgentReportMapper.Map(request, DateTime.UtcNow);

        Assert.Equal(timestamp, report.TimestampUtc);
        Assert.Single(report.Drives);
        Assert.Single(report.Alerts);
        Assert.Equal(timestamp, report.Drives[0].TimestampUtc);
        Assert.Equal(timestamp, report.Alerts[0].TimestampUtc);
    }

    [Fact]
    public void Map_UsesFallbackTimestampWhenMissing()
    {
        var fallback = new DateTime(2024, 2, 2, 8, 0, 0, DateTimeKind.Utc);
        var request = new AgentReportRequest
        {
            AgentId = "agent-2",
            TimestampUtc = default,
            Drives =
            {
                new DriveReportDto
                {
                    DriveLetter = "D:",
                    TotalSpaceGb = 1000,
                    FreeSpaceGb = 500,
                    UsedPercent = 50
                }
            }
        };

        var report = AgentReportMapper.Map(request, fallback);

        Assert.Equal(fallback, report.TimestampUtc);
        Assert.Single(report.Drives);
        Assert.Empty(report.Alerts);
    }
}

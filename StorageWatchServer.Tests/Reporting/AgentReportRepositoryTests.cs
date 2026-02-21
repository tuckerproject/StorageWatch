using StorageWatchServer.Server.Reporting.Data;
using StorageWatchServer.Server.Reporting.Models;
using StorageWatchServer.Tests.Utilities;
using Xunit;

namespace StorageWatchServer.Tests.Reporting;

public class AgentReportRepositoryTests
{
    [Fact]
    public async Task SaveReportAsync_PersistsDriveAndAlertRecords()
    {
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateAgentReportRepository();

        var report = new AgentReport
        {
            AgentId = "agent-1",
            TimestampUtc = DateTime.UtcNow,
            Drives =
            {
                new DriveReport
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    FreeSpaceGb = 200,
                    UsedPercent = 60,
                    TimestampUtc = DateTime.UtcNow
                }
            },
            Alerts =
            {
                new AlertRecord
                {
                    DriveLetter = "C:",
                    Level = "Warning",
                    Message = "Low space",
                    TimestampUtc = DateTime.UtcNow
                }
            }
        };

        await repository.SaveReportAsync(report);
        var reports = await repository.GetRecentReportsAsync(5);

        Assert.NotEmpty(reports);
        var saved = reports.FirstOrDefault(r => r.AgentId == report.AgentId);
        Assert.NotNull(saved);
        Assert.Single(saved!.Drives);
        Assert.Single(saved.Alerts);
    }

    [Fact]
    public async Task GetRecentReportsAsync_ReturnsReportsInDescendingOrder()
    {
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateAgentReportRepository();

        var older = new AgentReport
        {
            AgentId = "agent-1",
            TimestampUtc = DateTime.UtcNow.AddMinutes(-10),
            Drives =
            {
                new DriveReport
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    FreeSpaceGb = 250,
                    UsedPercent = 50,
                    TimestampUtc = DateTime.UtcNow.AddMinutes(-10)
                }
            }
        };

        var newer = new AgentReport
        {
            AgentId = "agent-1",
            TimestampUtc = DateTime.UtcNow,
            Drives =
            {
                new DriveReport
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    FreeSpaceGb = 150,
                    UsedPercent = 70,
                    TimestampUtc = DateTime.UtcNow
                }
            }
        };

        await repository.SaveReportAsync(older);
        await repository.SaveReportAsync(newer);

        var reports = await repository.GetRecentReportsAsync(1);

        Assert.Single(reports);
        Assert.Equal(newer.TimestampUtc, reports[0].TimestampUtc);
    }
}

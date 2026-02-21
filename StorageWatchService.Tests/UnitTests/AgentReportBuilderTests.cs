using FluentAssertions;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.CentralServer;
using StorageWatch.Services.Monitoring;
using StorageWatch.Tests.Utilities;

namespace StorageWatch.Tests.UnitTests
{
    public class AgentReportBuilderTests
    {
        [Fact]
        public void BuildReport_IncludesDriveReportsAndAlerts()
        {
            var options = new StorageWatchOptions
            {
                Monitoring = new MonitoringOptions
                {
                    ThresholdPercent = 20,
                    Drives = new List<string> { "C:", "D:" }
                }
            };

            var provider = new StubStatusProvider(new Dictionary<string, DiskStatus>
            {
                ["C:"] = new DiskStatus { DriveName = "C:", TotalSpaceGb = 500, FreeSpaceGb = 50 },
                ["D:"] = new DiskStatus { DriveName = "D:", TotalSpaceGb = 0, FreeSpaceGb = 0 }
            });

            var builder = new AgentReportBuilder(new TestOptionsMonitor<StorageWatchOptions>(options), provider);

            var report = builder.BuildReport("agent-1", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            report.AgentId.Should().Be("agent-1");
            report.Drives.Should().HaveCount(1);
            report.Drives[0].DriveLetter.Should().Be("C:");
            report.Drives[0].UsedPercent.Should().Be(90);

            report.Alerts.Should().HaveCount(2);
            report.Alerts.Should().Contain(alert => alert.DriveLetter == "C:" && alert.Level == "Warning");
            report.Alerts.Should().Contain(alert => alert.DriveLetter == "D:" && alert.Level == "Error");
        }

        private sealed class StubStatusProvider : IDiskStatusProvider
        {
            private readonly IReadOnlyDictionary<string, DiskStatus> _statuses;

            public StubStatusProvider(IReadOnlyDictionary<string, DiskStatus> statuses)
            {
                _statuses = statuses;
            }

            public DiskStatus GetStatus(string driveLetter)
            {
                return _statuses.TryGetValue(driveLetter, out var status)
                    ? status
                    : new DiskStatus { DriveName = driveLetter, TotalSpaceGb = 0, FreeSpaceGb = 0 };
            }
        }
    }
}

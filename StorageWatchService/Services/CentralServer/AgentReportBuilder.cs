using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Monitoring;
using Microsoft.Extensions.Options;

namespace StorageWatch.Services.CentralServer
{
    public class AgentReportBuilder
    {
        private readonly IOptionsMonitor<StorageWatchOptions> _optionsMonitor;
        private readonly IDiskStatusProvider _statusProvider;

        public AgentReportBuilder(IOptionsMonitor<StorageWatchOptions> optionsMonitor, IDiskStatusProvider statusProvider)
        {
            _optionsMonitor = optionsMonitor;
            _statusProvider = statusProvider;
        }

        public AgentReportRequest BuildReport(string agentId, DateTime timestampUtc)
        {
            var options = _optionsMonitor.CurrentValue;
            var report = new AgentReportRequest
            {
                AgentId = agentId,
                TimestampUtc = timestampUtc
            };

            foreach (var driveLetter in options.Monitoring.Drives)
            {
                var status = _statusProvider.GetStatus(driveLetter);

                if (status.TotalSpaceGb <= 0)
                {
                    report.Alerts.Add(new AlertDto
                    {
                        DriveLetter = driveLetter,
                        Level = "Error",
                        Message = $"Drive {driveLetter} is not ready or unavailable."
                    });
                    continue;
                }

                report.Drives.Add(new DriveReportDto
                {
                    DriveLetter = status.DriveName,
                    TotalSpaceGb = status.TotalSpaceGb,
                    FreeSpaceGb = status.FreeSpaceGb,
                    UsedPercent = Math.Round(100 - status.PercentFree, 2)
                });

                if (status.PercentFree < options.Monitoring.ThresholdPercent)
                {
                    report.Alerts.Add(new AlertDto
                    {
                        DriveLetter = status.DriveName,
                        Level = "Warning",
                        Message = $"Drive {status.DriveName} below threshold. {status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%)."
                    });
                }
            }

            return report;
        }
    }
}

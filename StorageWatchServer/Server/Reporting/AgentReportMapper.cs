using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Reporting.Models;

namespace StorageWatchServer.Server.Reporting;

public static class AgentReportMapper
{
    public static AgentReport Map(AgentReportRequest request, DateTime fallbackTimestampUtc)
    {
        var timestamp = request.TimestampUtc == default ? fallbackTimestampUtc : request.TimestampUtc;

        var report = new AgentReport
        {
            AgentId = request.AgentId,
            TimestampUtc = timestamp
        };

        foreach (var drive in request.Drives)
        {
            report.Drives.Add(new DriveReport
            {
                DriveLetter = drive.DriveLetter,
                TotalSpaceGb = drive.TotalSpaceGb,
                FreeSpaceGb = drive.FreeSpaceGb,
                UsedPercent = drive.UsedPercent,
                TimestampUtc = timestamp
            });
        }

        foreach (var alert in request.Alerts)
        {
            report.Alerts.Add(new AlertRecord
            {
                DriveLetter = alert.DriveLetter,
                Level = alert.Level,
                Message = alert.Message,
                TimestampUtc = timestamp
            });
        }

        return report;
    }
}

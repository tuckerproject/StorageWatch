using StorageWatchServer.Server.Reporting.Models;

namespace StorageWatchServer.Server.Reporting.Data;

public interface IAgentReportRepository
{
    Task SaveReportAsync(AgentReport report);

    Task<IReadOnlyList<AgentReport>> GetRecentReportsAsync(int count);
}

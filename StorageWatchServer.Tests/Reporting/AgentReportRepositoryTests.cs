using StorageWatchServer.Tests.Utilities;
using Xunit;

namespace StorageWatchServer.Tests.Reporting;

public class AgentReportRepositoryTests
{
    // Tests for the old AgentReportRepository have been deprecated.
    // This class will be rewritten to test RawRowIngestionService.
    // Keeping the namespace and class name for now to avoid breaking test discovery.

    [Fact(Skip = "Old ingestion pipeline tests - to be rewritten for new architecture")]
    public async Task SaveReportAsync_PersistsDriveAndAlertRecords()
    {
        await Task.CompletedTask;
    }
}

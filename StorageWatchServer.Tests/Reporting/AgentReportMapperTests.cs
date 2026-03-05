using Xunit;

namespace StorageWatchServer.Tests.Reporting;

public class AgentReportMapperTests
{
    // Tests for the old AgentReportMapper have been deprecated.
    // This class was testing the old ingestion pipeline and will be rewritten
    // to test RawRowIngestionService with the new consolidated database architecture.

    [Fact(Skip = "Old mapper tests - to be rewritten for new architecture")]
    public void Map_UsesRequestTimestampWhenProvided()
    {
        // Old test - no longer applicable
    }

    [Fact(Skip = "Old mapper tests - to be rewritten for new architecture")]
    public void Map_UsesFallbackTimestampWhenMissing()
    {
        // Old test - no longer applicable
    }
}

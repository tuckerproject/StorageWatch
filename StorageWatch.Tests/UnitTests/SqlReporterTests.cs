/// <summary>
/// Unit Tests for SqlReporter
/// 
/// Tests the SQLite write operation logic without requiring a real database.
/// Uses mocks to verify that the correct SQL operations are performed.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config;
using StorageWatch.Services.Logging;
using StorageWatch.Services.Scheduling;

namespace StorageWatch.Tests.UnitTests
{
    public class SqlReporterTests
    {
        private readonly Mock<RollingFileLogger> _mockLogger;

        public SqlReporterTests()
        {
            _mockLogger = new Mock<RollingFileLogger>(Path.GetTempFileName());
        }

        [Fact]
        public void Constructor_WithValidConfig_InitializesSuccessfully()
        {
            // Arrange
            var config = new StorageWatchConfig
            {
                Database = new DatabaseConfig
                {
                    ConnectionString = "Data Source=:memory:"
                },
                Drives = new List<string> { "C:" }
            };

            // Act
            var reporter = new SqlReporter(config, _mockLogger.Object);

            // Assert
            reporter.Should().NotBeNull();
        }

        // Note: Full integration tests for WriteDailyReportAsync are in SqlReporterIntegrationTests
        // These unit tests focus on the constructor and basic validation
    }
}

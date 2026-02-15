/// <summary>
/// Integration Tests for SqlReporter
/// 
/// Tests the full SQL reporting pipeline with a real SQLite database.
/// Verifies that disk space data is correctly written to the database.
/// </summary>

using FluentAssertions;
using Microsoft.Data.Sqlite;
using StorageWatch.Config.Options;
using StorageWatch.Data;
using StorageWatch.Services.Logging;
using StorageWatch.Services.Scheduling;
using StorageWatch.Tests.Utilities;

namespace StorageWatch.Tests.IntegrationTests
{
    public class SqlReporterIntegrationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly string _connectionString;
        private readonly RollingFileLogger _logger;
        private readonly StorageWatchOptions _config;

        public SqlReporterIntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_reporter_{Guid.NewGuid()}.db");
            _connectionString = $"Data Source={_testDbPath}";
            _logger = new RollingFileLogger(Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log"));

            _config = TestHelpers.CreateDefaultTestConfig();
            _config.Database.ConnectionString = _connectionString;

            // Initialize the database schema
            var schema = new SqliteSchema(_connectionString, _logger);
            schema.InitializeDatabaseAsync().Wait();
        }

        [Fact]
        public async Task WriteDailyReportAsync_InsertsRecordIntoDatabase()
        {
            // Arrange
            var reporter = new SqlReporter(_config, _logger);

            // Act
            await reporter.WriteDailyReportAsync();

            // Assert - Verify record was inserted
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("SELECT COUNT(*) FROM DiskSpaceLog", connection);
            var count = await command.ExecuteScalarAsync();

            Convert.ToInt32(count).Should().BeGreaterThan(0, "At least one record should be inserted");
        }

        [Fact]
        public async Task WriteDailyReportAsync_InsertsCorrectMachineName()
        {
            // Arrange
            var reporter = new SqlReporter(_config, _logger);
            var expectedMachineName = Environment.MachineName;

            // Act
            await reporter.WriteDailyReportAsync();

            // Assert
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("SELECT MachineName FROM DiskSpaceLog LIMIT 1", connection);
            var machineName = await command.ExecuteScalarAsync();

            machineName?.ToString().Should().Be(expectedMachineName);
        }

        [Fact]
        public async Task WriteDailyReportAsync_InsertsCorrectDriveLetter()
        {
            // Arrange
            var reporter = new SqlReporter(_config, _logger);

            // Act
            await reporter.WriteDailyReportAsync();

            // Assert
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("SELECT DriveLetter FROM DiskSpaceLog LIMIT 1", connection);
            var driveLetter = await command.ExecuteScalarAsync();

            driveLetter?.ToString().Should().Be("C:");
        }

        [Fact]
        public async Task WriteDailyReportAsync_InsertsValidSpaceMetrics()
        {
            // Arrange
            var reporter = new SqlReporter(_config, _logger);

            // Act
            await reporter.WriteDailyReportAsync();

            // Assert
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand(
                "SELECT TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree FROM DiskSpaceLog LIMIT 1",
                connection
            );
            var reader = await command.ExecuteReaderAsync();

            reader.Read().Should().BeTrue();

            var totalSpace = reader.GetDouble(0);
            var usedSpace = reader.GetDouble(1);
            var freeSpace = reader.GetDouble(2);
            var percentFree = reader.GetDouble(3);

            totalSpace.Should().BeGreaterThan(0);
            freeSpace.Should().BeGreaterOrEqualTo(0);
            freeSpace.Should().BeLessOrEqualTo(totalSpace);
            usedSpace.Should().BeGreaterOrEqualTo(0);
            percentFree.Should().BeInRange(0, 100);
        }

        [Fact]
        public async Task WriteDailyReportAsync_SetsUtcTimestamp()
        {
            // Arrange
            var reporter = new SqlReporter(_config, _logger);
            var beforeUtc = DateTime.UtcNow.AddSeconds(-1);

            // Act
            await reporter.WriteDailyReportAsync();

            var afterUtc = DateTime.UtcNow.AddSeconds(1);

            // Assert
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("SELECT CollectionTimeUtc FROM DiskSpaceLog LIMIT 1", connection);
            var timestamp = await command.ExecuteScalarAsync();

            var parsedTime = DateTime.Parse(timestamp.ToString()!);
            parsedTime.Should().BeAfter(beforeUtc);
            parsedTime.Should().BeBefore(afterUtc);
        }

        [Fact]
        public async Task WriteDailyReportAsync_WithMultipleDrives_InsertsMultipleRecords()
        {
            // Arrange
            _config.Monitoring.Drives = new List<string> { "C:", "D:" };
            var reporter = new SqlReporter(_config, _logger);

            // Act
            await reporter.WriteDailyReportAsync();

            // Assert
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("SELECT COUNT(DISTINCT DriveLetter) FROM DiskSpaceLog", connection);
            var count = await command.ExecuteScalarAsync();

            // At least C: should be inserted (D: might not exist on test machine)
            Convert.ToInt32(count).Should().BeGreaterOrEqualTo(1);
        }

        [Fact]
        public async Task WriteDailyReportAsync_CalledMultipleTimes_InsertsMultipleRecords()
        {
            // Arrange
            var reporter = new SqlReporter(_config, _logger);

            // Act
            await reporter.WriteDailyReportAsync();
            await Task.Delay(100); // Small delay to ensure different timestamps
            await reporter.WriteDailyReportAsync();

            // Assert
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("SELECT COUNT(*) FROM DiskSpaceLog", connection);
            var count = await command.ExecuteScalarAsync();

            Convert.ToInt32(count).Should().BeGreaterOrEqualTo(2);
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}

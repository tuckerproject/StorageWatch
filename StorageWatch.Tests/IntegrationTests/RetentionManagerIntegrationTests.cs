/// <summary>
/// Integration Tests for RetentionManager
/// 
/// Tests the full retention and cleanup operations with a real SQLite database.
/// Verifies that old rows are deleted, row counts are trimmed, and archiving works.
/// </summary>

using FluentAssertions;
using Microsoft.Data.Sqlite;
using StorageWatch.Config.Options;
using StorageWatch.Data;
using StorageWatch.Services.DataRetention;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;

namespace StorageWatch.Tests.IntegrationTests
{
    public class RetentionManagerIntegrationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly string _connectionString;
        private readonly string _archiveDir;
        private readonly RollingFileLogger _logger;
        private readonly StorageWatchOptions _config;

        public RetentionManagerIntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_retention_{Guid.NewGuid()}.db");
            _connectionString = $"Data Source={_testDbPath}";
            _archiveDir = Path.Combine(Path.GetTempPath(), $"test_archive_{Guid.NewGuid()}");
            _logger = new RollingFileLogger(Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log"));

            _config = TestHelpers.CreateDefaultTestConfig();
            _config.Database.ConnectionString = _connectionString;

            // Initialize the database schema
            var schema = new SqliteSchema(_connectionString, _logger);
            schema.InitializeDatabaseAsync().Wait();
        }

        [Fact]
        public async Task CheckAndCleanupAsync_RemovesRowsOlderThanMaxDays()
        {
            // Arrange
            var retentionOptions = new RetentionOptions
            {
                Enabled = true,
                MaxDays = 7,  // Keep only 7 days
                MaxRows = 0,
                CleanupIntervalMinutes = 1,
                ArchiveEnabled = false
            };

            var manager = new RetentionManager(_connectionString, retentionOptions, _logger);

            // Insert test data: old and recent rows
            await InsertTestDataAsync(
                machineCount: 1,
                drivesPerMachine: 1,
                rowsPerDrive: 10,
                daysOld: 30  // 30 days old
            );

            var countBefore = await GetRowCountAsync();
            countBefore.Should().BeGreaterThan(0);

            // Act
            var cleaned = await manager.CheckAndCleanupAsync();

            // Assert
            cleaned.Should().BeTrue("Cleanup should have been performed");

            var countAfter = await GetRowCountAsync();
            countAfter.Should().BeLessThan(countBefore, "Old rows should have been deleted");
        }

        [Fact]
        public async Task CheckAndCleanupAsync_RespectsMaxRowsLimit()
        {
            // Arrange
            const int maxRows = 50;
            var retentionOptions = new RetentionOptions
            {
                Enabled = true,
                MaxDays = 3650,  // Very large value so MaxDays doesn't trigger
                MaxRows = maxRows,
                CleanupIntervalMinutes = 1,
                ArchiveEnabled = false
            };

            var manager = new RetentionManager(_connectionString, retentionOptions, _logger);

            // Insert many rows (over the limit)
            await InsertTestDataAsync(
                machineCount: 2,
                drivesPerMachine: 2,
                rowsPerDrive: 20,  // Total: 2 * 2 * 20 = 80 rows
                daysOld: 0
            );

            var countBefore = await GetRowCountAsync();
            countBefore.Should().BeGreaterThan(maxRows);

            // Act
            var cleaned = await manager.CheckAndCleanupAsync();

            // Assert
            cleaned.Should().BeTrue("Cleanup should have been performed");

            var countAfter = await GetRowCountAsync();
            countAfter.Should().BeLessThanOrEqualTo(maxRows, "Row count should be trimmed to MaxRows");
        }

        [Fact]
        public async Task CheckAndCleanupAsync_ArchivesToCsvAndDeletes()
        {
            // Arrange
            Directory.CreateDirectory(_archiveDir);

            var retentionOptions = new RetentionOptions
            {
                Enabled = true,
                MaxDays = 7,
                MaxRows = 0,
                CleanupIntervalMinutes = 1,
                ArchiveEnabled = true,
                ArchiveDirectory = _archiveDir,
                ExportCsvEnabled = true
            };

            var manager = new RetentionManager(_connectionString, retentionOptions, _logger);

            // Insert old test data
            await InsertTestDataAsync(
                machineCount: 1,
                drivesPerMachine: 1,
                rowsPerDrive: 5,
                daysOld: 30
            );

            var countBefore = await GetRowCountAsync();

            // Act
            var cleaned = await manager.CheckAndCleanupAsync();

            // Assert
            cleaned.Should().BeTrue("Cleanup should have been performed");

            // Check that CSV files were created
            var csvFiles = Directory.GetFiles(_archiveDir, "*.csv");
            csvFiles.Should().NotBeEmpty("CSV archive file should have been created");

            // Check that old rows were deleted
            var countAfter = await GetRowCountAsync();
            countAfter.Should().BeLessThan(countBefore, "Old rows should have been archived and deleted");

            // Verify CSV content has headers and data
            var csvContent = await File.ReadAllTextAsync(csvFiles[0]);
            csvContent.Should().Contain("Id,MachineName,DriveLetter", "CSV should have proper headers");
        }

        [Fact]
        public async Task CheckAndCleanupAsync_WhenDisabled_DoesNotDelete()
        {
            // Arrange
            var retentionOptions = new RetentionOptions
            {
                Enabled = false,  // Disabled
                MaxDays = 1,
                CleanupIntervalMinutes = 1
            };

            var manager = new RetentionManager(_connectionString, retentionOptions, _logger);

            // Insert old test data
            await InsertTestDataAsync(
                machineCount: 1,
                drivesPerMachine: 1,
                rowsPerDrive: 5,
                daysOld: 30
            );

            var countBefore = await GetRowCountAsync();

            // Act
            var cleaned = await manager.CheckAndCleanupAsync();

            // Assert
            cleaned.Should().BeFalse("Cleanup should be skipped when disabled");

            var countAfter = await GetRowCountAsync();
            countAfter.Should().Be(countBefore, "No rows should be deleted when retention is disabled");
        }

        [Fact]
        public async Task CheckAndCleanupAsync_DoesNotRunBeforeIntervalElapsed()
        {
            // Arrange
            var retentionOptions = new RetentionOptions
            {
                Enabled = true,
                MaxDays = 1,
                CleanupIntervalMinutes = 60,  // 60 minute interval
                ArchiveEnabled = false
            };

            var manager = new RetentionManager(_connectionString, retentionOptions, _logger);

            // Insert old test data
            await InsertTestDataAsync(
                machineCount: 1,
                drivesPerMachine: 1,
                rowsPerDrive: 5,
                daysOld: 30
            );

            var countBefore = await GetRowCountAsync();

            // Act - Call twice
            var result1 = await manager.CheckAndCleanupAsync();
            var result2 = await manager.CheckAndCleanupAsync();

            // Assert
            // First call should attempt cleanup, second should skip
            result2.Should().BeFalse("Second call within interval should be skipped");

            var countAfter = await GetRowCountAsync();
            // Count may be different after first call, but should be same after second
        }

        [Fact]
        public async Task CheckAndCleanupAsync_CsvExportFormatIsValid()
        {
            // Arrange
            Directory.CreateDirectory(_archiveDir);

            var retentionOptions = new RetentionOptions
            {
                Enabled = true,
                MaxDays = 7,
                MaxRows = 0,
                CleanupIntervalMinutes = 1,
                ArchiveEnabled = true,
                ArchiveDirectory = _archiveDir,
                ExportCsvEnabled = true
            };

            var manager = new RetentionManager(_connectionString, retentionOptions, _logger);

            // Insert test data with known values
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO DiskSpaceLog 
                (MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc)
                VALUES (@machine, @drive, @total, @used, @free, @percent, @utc)
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@machine", "TestMachine");
            command.Parameters.AddWithValue("@drive", "C:");
            command.Parameters.AddWithValue("@total", 1000.5);
            command.Parameters.AddWithValue("@used", 600.3);
            command.Parameters.AddWithValue("@free", 400.2);
            command.Parameters.AddWithValue("@percent", 40.02);
            command.Parameters.AddWithValue("@utc", DateTime.UtcNow.AddDays(-30));

            await command.ExecuteNonQueryAsync();

            // Act
            var cleaned = await manager.CheckAndCleanupAsync();

            // Assert
            cleaned.Should().BeTrue();

            var csvFiles = Directory.GetFiles(_archiveDir, "*.csv");
            csvFiles.Should().HaveCount(1);

            var csvContent = await File.ReadAllTextAsync(csvFiles[0]);
            var lines = csvContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            lines.Should().HaveCountGreaterThanOrEqualTo(2, "CSV should have header + at least 1 data row");
            lines[0].Should().Contain("CollectionTimeUtc");
            lines[0].Should().Contain("PercentFree");
        }

        private async Task InsertTestDataAsync(int machineCount, int drivesPerMachine, int rowsPerDrive, int daysOld)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

            for (int m = 0; m < machineCount; m++)
            {
                for (int d = 0; d < drivesPerMachine; d++)
                {
                    for (int r = 0; r < rowsPerDrive; r++)
                    {
                        var sql = @"
                            INSERT INTO DiskSpaceLog 
                            (MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc)
                            VALUES (@machine, @drive, @total, @used, @free, @percent, @utc)
                        ";

                        using var command = new SqliteCommand(sql, connection);
                        command.Parameters.AddWithValue("@machine", $"Machine{m}");
                        command.Parameters.AddWithValue("@drive", $"{(char)('C' + d)}:");
                        command.Parameters.AddWithValue("@total", 1000.0);
                        command.Parameters.AddWithValue("@used", 600.0);
                        command.Parameters.AddWithValue("@free", 400.0);
                        command.Parameters.AddWithValue("@percent", 40.0);
                        command.Parameters.AddWithValue("@utc", cutoffDate.AddHours(r));

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private async Task<int> GetRowCountAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("SELECT COUNT(*) FROM DiskSpaceLog", connection);
            var count = await command.ExecuteScalarAsync();

            return Convert.ToInt32(count);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath))
                    File.Delete(_testDbPath);

                if (Directory.Exists(_archiveDir))
                    Directory.Delete(_archiveDir, true);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }
}

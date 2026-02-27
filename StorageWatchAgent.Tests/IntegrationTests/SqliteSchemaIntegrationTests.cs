/// <summary>
/// Integration Tests for SqliteSchema
/// 
/// Tests the actual SQLite database schema creation and validation.
/// These tests use a real in-memory SQLite database to verify schema correctness.
/// </summary>

using FluentAssertions;
using Microsoft.Data.Sqlite;
using StorageWatch.Data;
using StorageWatch.Services.Logging;

namespace StorageWatch.Tests.IntegrationTests
{
    public class SqliteSchemaIntegrationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly string _connectionString;
        private readonly RollingFileLogger _logger;

        public SqliteSchemaIntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_storage_watch_{Guid.NewGuid()}.db");
            _connectionString = $"Data Source={_testDbPath}";
            _logger = new RollingFileLogger(Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log"));
        }

        [Fact]
        public async Task InitializeDatabaseAsync_CreatesDatabase_Successfully()
        {
            // Arrange
            var schema = new SqliteSchema(_connectionString, _logger);

            // Act
            await schema.InitializeDatabaseAsync();

            // Assert
            File.Exists(_testDbPath).Should().BeTrue("Database file should be created");
        }

        [Fact]
        public async Task InitializeDatabaseAsync_CreatesDiskSpaceLogTable()
        {
            // Arrange
            var schema = new SqliteSchema(_connectionString, _logger);

            // Act
            await schema.InitializeDatabaseAsync();

            // Assert - Verify table exists by querying it
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='DiskSpaceLog'",
                connection
            );
            var result = await command.ExecuteScalarAsync();

            result.Should().NotBeNull();
            result.Should().Be("DiskSpaceLog");
        }

        [Fact]
        public async Task InitializeDatabaseAsync_CreatesCorrectTableSchema()
        {
            // Arrange
            var schema = new SqliteSchema(_connectionString, _logger);

            // Act
            await schema.InitializeDatabaseAsync();

            // Assert - Verify table structure
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand("PRAGMA table_info(DiskSpaceLog)", connection);
            var reader = await command.ExecuteReaderAsync();

            var columns = new List<string>();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1)); // Column name is at index 1
            }

            columns.Should().Contain("Id");
            columns.Should().Contain("MachineName");
            columns.Should().Contain("DriveLetter");
            columns.Should().Contain("TotalSpaceGB");
            columns.Should().Contain("UsedSpaceGB");
            columns.Should().Contain("FreeSpaceGB");
            columns.Should().Contain("PercentFree");
            columns.Should().Contain("CollectionTimeUtc");
            columns.Should().Contain("CreatedAt");
        }

        [Fact]
        public async Task InitializeDatabaseAsync_CreatesIndex()
        {
            // Arrange
            var schema = new SqliteSchema(_connectionString, _logger);

            // Act
            await schema.InitializeDatabaseAsync();

            // Assert - Verify index exists
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand(
                "SELECT name FROM sqlite_master WHERE type='index' AND name='idx_DiskSpaceLog_Machine_Drive_Time'",
                connection
            );
            var result = await command.ExecuteScalarAsync();

            result.Should().NotBeNull();
            result.Should().Be("idx_DiskSpaceLog_Machine_Drive_Time");
        }

        [Fact]
        public async Task InitializeDatabaseAsync_CalledMultipleTimes_IsIdempotent()
        {
            // Arrange
            var schema = new SqliteSchema(_connectionString, _logger);

            // Act - Initialize multiple times
            await schema.InitializeDatabaseAsync();
            await schema.InitializeDatabaseAsync();
            await schema.InitializeDatabaseAsync();

            // Assert - Should not throw and database should still be valid
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = new SqliteCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='DiskSpaceLog'",
                connection
            );
            var count = await command.ExecuteScalarAsync();

            Convert.ToInt32(count).Should().Be(1, "Table should only be created once");
        }

        [Fact]
        public async Task InitializeDatabaseAsync_WithInMemoryDatabase_Works()
        {
            // Arrange
            // Note: In-memory SQLite databases are ephemeral - they only exist while
            // a connection to them is open. Since SqliteSchema opens and closes its own
            // connection, we can't verify table existence in a separate connection.
            // This test simply verifies that initialization completes without throwing.
            var inMemoryConnectionString = "Data Source=:memory:";
            var schema = new SqliteSchema(inMemoryConnectionString, _logger);

            // Act & Assert - Should complete without throwing
            Func<Task> act = async () => await schema.InitializeDatabaseAsync();

            await act.Should().NotThrowAsync("Schema initialization should work with in-memory databases");
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

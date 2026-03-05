using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Services;
using Xunit;

namespace StorageWatchServer.Tests.Data;

/// <summary>
/// Tests for database schema initialization and auto-creation.
/// </summary>
public class DatabaseSchemaTests
{
    [Fact]
    public async Task InitializeDatabaseAsync_CreatesRawDriveRowsTable()
    {
        // Arrange
        var databaseId = Guid.NewGuid().ToString("N")[..8];
        var options = new ServerOptions
        {
            ListenUrl = "http://localhost:5001",
            DatabasePath = $"file:memdb_schema_{databaseId}?mode=memory&cache=shared",
            OnlineTimeoutMinutes = 5
        };

        var schema = new ServerSchema(options);

        // Act
        await schema.InitializeDatabaseAsync();

        // Assert - verify RawDriveRows table exists with correct columns
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath}");
        await connection.OpenAsync();

        const string query = @"
            PRAGMA table_info(RawDriveRows)";

        await using var command = new SqliteCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var columnNames = new List<string>();
        while (await reader.ReadAsync())
        {
            columnNames.Add(reader.GetString(1)); // Column name is at index 1
        }

        Assert.Contains("Id", columnNames);
        Assert.Contains("MachineName", columnNames);
        Assert.Contains("DriveLetter", columnNames);
        Assert.Contains("TotalSpaceGb", columnNames);
        Assert.Contains("UsedSpaceGb", columnNames);
        Assert.Contains("FreeSpaceGb", columnNames);
        Assert.Contains("PercentFree", columnNames);
        Assert.Contains("Timestamp", columnNames);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesMachinesTable()
    {
        // Arrange
        var databaseId = Guid.NewGuid().ToString("N")[..8];
        var options = new ServerOptions
        {
            ListenUrl = "http://localhost:5001",
            DatabasePath = $"file:memdb_machines_{databaseId}?mode=memory&cache=shared",
            OnlineTimeoutMinutes = 5
        };

        var schema = new ServerSchema(options);

        // Act
        await schema.InitializeDatabaseAsync();

        // Assert
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath}");
        await connection.OpenAsync();

        const string query = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' AND name='Machines'";

        await using var command = new SqliteCommand(query, connection);
        var result = await command.ExecuteScalarAsync();

        Assert.NotNull(result);
        Assert.Equal("Machines", result);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesSettingsTable()
    {
        // Arrange
        var databaseId = Guid.NewGuid().ToString("N")[..8];
        var options = new ServerOptions
        {
            ListenUrl = "http://localhost:5001",
            DatabasePath = $"file:memdb_settings_{databaseId}?mode=memory&cache=shared",
            OnlineTimeoutMinutes = 5
        };

        var schema = new ServerSchema(options);

        // Act
        await schema.InitializeDatabaseAsync();

        // Assert
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath}");
        await connection.OpenAsync();

        const string query = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' AND name='Settings'";

        await using var command = new SqliteCommand(query, connection);
        var result = await command.ExecuteScalarAsync();

        Assert.NotNull(result);
        Assert.Equal("Settings", result);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CreatesIndexOnRawDriveRows()
    {
        // Arrange
        var databaseId = Guid.NewGuid().ToString("N")[..8];
        var options = new ServerOptions
        {
            ListenUrl = "http://localhost:5001",
            DatabasePath = $"file:memdb_index_{databaseId}?mode=memory&cache=shared",
            OnlineTimeoutMinutes = 5
        };

        var schema = new ServerSchema(options);

        // Act
        await schema.InitializeDatabaseAsync();

        // Assert
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath}");
        await connection.OpenAsync();

        const string query = @"
            SELECT name FROM sqlite_master 
            WHERE type='index' AND name='idx_RawDriveRows_Machine_Time'";

        await using var command = new SqliteCommand(query, connection);
        var result = await command.ExecuteScalarAsync();

        Assert.NotNull(result);
        Assert.Equal("idx_RawDriveRows_Machine_Time", result);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_IsIdempotent()
    {
        // Arrange
        var databaseId = Guid.NewGuid().ToString("N")[..8];
        var options = new ServerOptions
        {
            ListenUrl = "http://localhost:5001",
            DatabasePath = $"file:memdb_idempotent_{databaseId}?mode=memory&cache=shared",
            OnlineTimeoutMinutes = 5
        };

        var schema = new ServerSchema(options);

        // Act - initialize twice
        await schema.InitializeDatabaseAsync();
        await schema.InitializeDatabaseAsync();

        // Assert - should not throw and database should still be valid
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath}");
        await connection.OpenAsync();

        const string query = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='RawDriveRows'";
        await using var command = new SqliteCommand(query, connection);
        var count = (long?)await command.ExecuteScalarAsync();

        Assert.Equal(1, count);
    }
}

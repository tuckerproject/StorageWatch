using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Reporting;
using StorageWatchServer.Tests.Utilities;
using Xunit;

namespace StorageWatchServer.Tests.Services;

/// <summary>
/// Unit tests for the RawRowIngestionService.
/// Validates that rows are inserted exactly as received with no transformations.
/// </summary>
public class RawRowIngestionServiceTests : IAsyncLifetime
{
    private TestDatabaseFactory? _factory;
    private RawRowIngestionService? _ingestionService;

    public async Task InitializeAsync()
    {
        _factory = await TestDatabaseFactory.CreateAsync();
        _ingestionService = _factory.GetIngestionService();
    }

    public async Task DisposeAsync()
    {
        await (_factory?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    [Fact]
    public async Task IngestRawRowsAsync_WithValidBatch_InsertsRowsAsIs()
    {
        // Arrange
        var machineName = "TestMachine";
        var now = DateTime.UtcNow;
        var rows = new List<RawDriveRow>
        {
            new()
            {
                MachineName = machineName,
                DriveLetter = "C:",
                TotalSpaceGb = 500.5,
                UsedSpaceGb = 250.25,
                FreeSpaceGb = 250.25,
                PercentFree = 50.05,
                Timestamp = now
            },
            new()
            {
                MachineName = machineName,
                DriveLetter = "D:",
                TotalSpaceGb = 1000.75,
                UsedSpaceGb = 600.5,
                FreeSpaceGb = 400.25,
                PercentFree = 40.025,
                Timestamp = now
            }
        };

        // Act
        await _ingestionService!.IngestRawRowsAsync(machineName, rows);

        // Assert - verify rows were inserted exactly as provided
        var serverOptions = _factory!.GetOptions();
        await using var connection = new SqliteConnection($"Data Source={serverOptions.DatabasePath}");
        await connection.OpenAsync();

        const string query = @"
            SELECT DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree
            FROM RawDriveRows
            WHERE MachineName = @machineName
            ORDER BY DriveLetter";

        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@machineName", machineName);

        await using var reader = await command.ExecuteReaderAsync();

        // Read first row (C:)
        Assert.True(await reader.ReadAsync());
        Assert.Equal("C:", reader.GetString(0));
        Assert.Equal(500.5, reader.GetDouble(1));
        Assert.Equal(250.25, reader.GetDouble(2));
        Assert.Equal(250.25, reader.GetDouble(3));
        Assert.Equal(50.05, reader.GetDouble(4));

        // Read second row (D:)
        Assert.True(await reader.ReadAsync());
        Assert.Equal("D:", reader.GetString(0));
        Assert.Equal(1000.75, reader.GetDouble(1));
        Assert.Equal(600.5, reader.GetDouble(2));
        Assert.Equal(400.25, reader.GetDouble(3));
        Assert.Equal(40.025, reader.GetDouble(4));

        // No more rows
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task IngestRawRowsAsync_WithEmptyMachineName_ThrowsArgumentException()
    {
        // Arrange
        var rows = new List<RawDriveRow>
        {
            new()
            {
                MachineName = "Machine",
                DriveLetter = "C:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 250,
                FreeSpaceGb = 250,
                PercentFree = 50,
                Timestamp = DateTime.UtcNow
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _ingestionService!.IngestRawRowsAsync("", rows));
    }

    [Fact]
    public async Task IngestRawRowsAsync_WithNullRows_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _ingestionService!.IngestRawRowsAsync("TestMachine", null!));
    }

    [Fact]
    public async Task IngestRawRowsAsync_WithEmptyRowsList_ThrowsArgumentException()
    {
        // Arrange
        var rows = new List<RawDriveRow>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _ingestionService!.IngestRawRowsAsync("TestMachine", rows));
    }

    [Fact]
    public async Task IngestRawRowsAsync_WithMultipleBatches_InsertsAllRows()
    {
        // Arrange
        var machineName = "TestMachine";
        var now = DateTime.UtcNow;

        var batch1 = new List<RawDriveRow>
        {
            new()
            {
                MachineName = machineName,
                DriveLetter = "C:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 250,
                FreeSpaceGb = 250,
                PercentFree = 50,
                Timestamp = now
            }
        };

        var batch2 = new List<RawDriveRow>
        {
            new()
            {
                MachineName = machineName,
                DriveLetter = "D:",
                TotalSpaceGb = 1000,
                UsedSpaceGb = 600,
                FreeSpaceGb = 400,
                PercentFree = 40,
                Timestamp = now.AddSeconds(1)
            }
        };

        // Act
        await _ingestionService!.IngestRawRowsAsync(machineName, batch1);
        await _ingestionService.IngestRawRowsAsync(machineName, batch2);

        // Assert
        var serverOptions = _factory!.GetOptions();
        await using var connection = new SqliteConnection($"Data Source={serverOptions.DatabasePath}");
        await connection.OpenAsync();

        const string countQuery = "SELECT COUNT(*) FROM RawDriveRows WHERE MachineName = @machineName";
        await using var countCommand = new SqliteCommand(countQuery, connection);
        countCommand.Parameters.AddWithValue("@machineName", machineName);
        var count = (long?)await countCommand.ExecuteScalarAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task IngestRawRowsAsync_PreservesTimestamp()
    {
        // Arrange
        var machineName = "TestMachine";
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
        var rows = new List<RawDriveRow>
        {
            new()
            {
                MachineName = machineName,
                DriveLetter = "C:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 250,
                FreeSpaceGb = 250,
                PercentFree = 50,
                Timestamp = specificTime
            }
        };

        // Act
        await _ingestionService!.IngestRawRowsAsync(machineName, rows);

        // Assert
        var serverOptions = _factory!.GetOptions();
        await using var connection = new SqliteConnection($"Data Source={serverOptions.DatabasePath}");
        await connection.OpenAsync();

        const string query = "SELECT Timestamp FROM RawDriveRows WHERE MachineName = @machineName";
        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@machineName", machineName);

        var result = await command.ExecuteScalarAsync();
        var storedTime = DateTime.Parse((string?)result ?? "");

        // Compare timestamps (allowing for minor precision differences in SQLite)
        Assert.True(Math.Abs((storedTime - specificTime).TotalSeconds) < 1);
    }
}

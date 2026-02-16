using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using StorageWatchUI.Services;
using System.IO;
using Xunit;

namespace StorageWatchUI.Tests.Services;

public class LocalDataProviderTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;

    public LocalDataProviderTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_testDbPath}";
        InitializeTestDatabase();
    }

    private void InitializeTestDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS DiskSpaceLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineName TEXT NOT NULL,
                DriveLetter TEXT NOT NULL,
                TotalSpaceGB REAL NOT NULL,
                UsedSpaceGB REAL NOT NULL,
                FreeSpaceGB REAL NOT NULL,
                PercentFree REAL NOT NULL,
                CollectionTimeUtc DATETIME NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );
        ";

        using var command = new SqliteCommand(createTableSql, connection);
        command.ExecuteNonQuery();

        var createHistorySql = @"
            CREATE TABLE IF NOT EXISTS DiskHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DriveLetter TEXT NOT NULL,
                PercentFree REAL NOT NULL,
                RecordedAtUtc DATETIME NOT NULL
            );
        ";

        using var historyCommand = new SqliteCommand(createHistorySql, connection);
        historyCommand.ExecuteNonQuery();

        // Insert test data
        var insertSql = @"
            INSERT INTO DiskSpaceLog (MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc)
            VALUES (@machine, @drive, @total, @used, @free, @percent, @time)
        ";

        using var insertCmd = new SqliteCommand(insertSql, connection);
        insertCmd.Parameters.AddWithValue("@machine", Environment.MachineName);
        insertCmd.Parameters.AddWithValue("@drive", "C:");
        insertCmd.Parameters.AddWithValue("@total", 500.0);
        insertCmd.Parameters.AddWithValue("@used", 400.0);
        insertCmd.Parameters.AddWithValue("@free", 100.0);
        insertCmd.Parameters.AddWithValue("@percent", 20.0);

        insertCmd.Parameters.AddWithValue("@time", DateTime.UtcNow.AddHours(-1));
        insertCmd.ExecuteNonQuery();

        insertCmd.Parameters["@time"].Value = DateTime.UtcNow;
        insertCmd.ExecuteNonQuery();

        var insertHistorySql = @"
            INSERT INTO DiskHistory (DriveLetter, PercentFree, RecordedAtUtc)
            VALUES (@drive, @percent, @time)
        ";

        using var historyInsertCmd = new SqliteCommand(insertHistorySql, connection);
        historyInsertCmd.Parameters.AddWithValue("@drive", "C:");
        historyInsertCmd.Parameters.AddWithValue("@percent", 20.0);
        historyInsertCmd.Parameters.AddWithValue("@time", DateTime.UtcNow);
        historyInsertCmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task GetCurrentDiskStatusAsync_WithData_ReturnsDisks()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatchUI:LocalDatabasePath", _testDbPath }
            })
            .Build();

        var provider = new LocalDataProvider(config);

        // Act
        var disks = await provider.GetCurrentDiskStatusAsync();

        // Assert
        disks.Should().HaveCount(1);
        disks.First().DriveName.Should().Be("C:");
        disks.First().TotalSpaceGb.Should().Be(500.0);
    }

    [Fact]
    public async Task GetCurrentDiskStatusAsync_WithNonExistentDatabase_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatchUI:LocalDatabasePath", nonExistentPath }
            })
            .Build();

        var provider = new LocalDataProvider(config);

        // Act
        var disks = await provider.GetCurrentDiskStatusAsync();

        // Assert
        disks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonitoredDrivesAsync_ReturnsDistinctDrives()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatchUI:LocalDatabasePath", _testDbPath }
            })
            .Build();

        var provider = new LocalDataProvider(config);

        // Act
        var drives = await provider.GetMonitoredDrivesAsync();

        // Assert
        drives.Should().Contain("C:");
    }

    [Fact]
    public async Task GetMonitoredDrivesAsync_WithNonExistentDatabase_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatchUI:LocalDatabasePath", nonExistentPath }
            })
            .Build();

        var provider = new LocalDataProvider(config);

        // Act
        var drives = await provider.GetMonitoredDrivesAsync();

        // Assert
        drives.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrendDataAsync_ReturnsHistoricalData()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatchUI:LocalDatabasePath", _testDbPath }
            })
            .Build();

        var provider = new LocalDataProvider(config);

        // Act
        var trends = await provider.GetTrendDataAsync("C:", 7);

        // Assert
        trends.Should().HaveCountGreaterThan(0);
        trends.First().DriveName.Should().Be("C:");
    }

    [Fact]
    public async Task GetTrendDataAsync_WithInvalidDrive_ReturnsEmptyList()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatchUI:LocalDatabasePath", _testDbPath }
            })
            .Build();

        var provider = new LocalDataProvider(config);

        // Act
        var trends = await provider.GetTrendDataAsync("Z:", 7);

        // Assert
        trends.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrendDataAsync_WithNonExistentDatabase_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatchUI:LocalDatabasePath", nonExistentPath }
            })
            .Build();

        var provider = new LocalDataProvider(config);

        // Act
        var trends = await provider.GetTrendDataAsync("C:", 7);

        // Assert
        trends.Should().BeEmpty();
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

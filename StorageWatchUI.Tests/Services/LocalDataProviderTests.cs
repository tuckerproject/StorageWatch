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
        _connectionString = $"Data Source={_testDbPath};Version=3;";
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
        insertCmd.Parameters.AddWithValue("@time", DateTime.UtcNow);
        insertCmd.ExecuteNonQuery();
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

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }
}

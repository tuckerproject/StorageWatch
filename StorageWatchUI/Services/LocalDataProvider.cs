using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using StorageWatchUI.Models;
using System.IO;

namespace StorageWatchUI.Services;

/// <summary>
/// Provides data access to the local SQLite database.
/// </summary>
public class LocalDataProvider : IDataProvider
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;

    public LocalDataProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        
        // Try to find the database in the expected locations
        var dbPath = GetDatabasePath();
        _connectionString = $"Data Source={dbPath};Version=3;";
    }

    private string GetDatabasePath()
    {
        // Try ProgramData first
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dbPath = Path.Combine(programData, "StorageWatch", "StorageWatch.db");
        
        if (File.Exists(dbPath))
            return dbPath;

        // Try current directory
        dbPath = Path.Combine(Directory.GetCurrentDirectory(), "StorageWatch.db");
        if (File.Exists(dbPath))
            return dbPath;

        // Try configured path
        var configPath = _configuration["StorageWatchUI:LocalDatabasePath"];
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            return configPath;

        // Default to ProgramData location even if it doesn't exist yet
        return Path.Combine(programData, "StorageWatch", "StorageWatch.db");
    }

    public async Task<List<DiskInfo>> GetCurrentDiskStatusAsync()
    {
        var disks = new List<DiskInfo>();

        try
        {
            if (!File.Exists(_connectionString.Replace("Data Source=", "").Split(';')[0]))
            {
                return disks; // Database doesn't exist yet
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get the latest entry for each drive
            var sql = @"
                SELECT 
                    DriveLetter,
                    TotalSpaceGB,
                    FreeSpaceGB,
                    PercentFree,
                    CollectionTimeUtc
                FROM DiskSpaceLog
                WHERE (DriveLetter, CollectionTimeUtc) IN (
                    SELECT DriveLetter, MAX(CollectionTimeUtc)
                    FROM DiskSpaceLog
                    WHERE MachineName = @machineName
                    GROUP BY DriveLetter
                )
                ORDER BY DriveLetter
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@machineName", Environment.MachineName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var totalSpace = reader.GetDouble(1);
                var freeSpace = reader.GetDouble(2);
                var percentFree = reader.GetDouble(3);
                var collectionTime = reader.GetDateTime(4);

                var disk = new DiskInfo
                {
                    DriveName = reader.GetString(0),
                    TotalSpaceGb = totalSpace,
                    FreeSpaceGb = freeSpace,
                    Status = DiskInfo.CalculateStatus(percentFree),
                    LastUpdated = collectionTime
                };

                disks.Add(disk);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching disk status: {ex}");
        }

        return disks;
    }

    public async Task<List<TrendDataPoint>> GetTrendDataAsync(string driveName, int daysBack = 7)
    {
        var trends = new List<TrendDataPoint>();

        try
        {
            if (!File.Exists(_connectionString.Replace("Data Source=", "").Split(';')[0]))
            {
                return trends;
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    CollectionTimeUtc,
                    DriveLetter,
                    PercentFree,
                    FreeSpaceGB,
                    TotalSpaceGB
                FROM DiskSpaceLog
                WHERE MachineName = @machineName
                  AND DriveLetter = @driveLetter
                  AND CollectionTimeUtc >= @startDate
                ORDER BY CollectionTimeUtc ASC
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@machineName", Environment.MachineName);
            command.Parameters.AddWithValue("@driveLetter", driveName);
            command.Parameters.AddWithValue("@startDate", DateTime.UtcNow.AddDays(-daysBack));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                trends.Add(new TrendDataPoint
                {
                    Timestamp = reader.GetDateTime(0),
                    DriveName = reader.GetString(1),
                    PercentFree = reader.GetDouble(2),
                    FreeSpaceGb = reader.GetDouble(3),
                    TotalSpaceGb = reader.GetDouble(4)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching trend data: {ex}");
        }

        return trends;
    }

    public async Task<List<string>> GetMonitoredDrivesAsync()
    {
        var drives = new List<string>();

        try
        {
            if (!File.Exists(_connectionString.Replace("Data Source=", "").Split(';')[0]))
            {
                return drives;
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT DISTINCT DriveLetter
                FROM DiskSpaceLog
                WHERE MachineName = @machineName
                ORDER BY DriveLetter
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@machineName", Environment.MachineName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                drives.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching monitored drives: {ex}");
        }

        return drives;
    }
}

using Microsoft.Data.Sqlite;
using StorageWatchUI.Models;
using StorageWatchUI.Services.Logging;
using System.IO;

namespace StorageWatchUI.Services;

/// <summary>
/// Provides data access to the local SQLite database.
/// </summary>
public class LocalDataProvider : IDataProvider
{
    private readonly string _connectionString;
    private readonly RollingFileLogger? _logger;

    public LocalDataProvider(IPathProvider pathProvider, RollingFileLogger? logger = null)
    {
        var dbPath = pathProvider.DatabasePath;
        _connectionString = $"Data Source={dbPath}";
        _logger = logger;
    }

    public async Task<List<DiskInfo>> GetCurrentDiskStatusAsync()
    {
        var disks = new List<DiskInfo>();

        try
        {
            var dbPath = _connectionString.Replace("Data Source=", "").Split(';')[0];
            if (!File.Exists(dbPath))
            {
                return disks; // Database doesn't exist yet
            }

            _logger?.Log("[DB] Querying local Agent database: GetCurrentDiskStatus");

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

            _logger?.Log($"[DB] Retrieved {disks.Count} rows from DiskSpaceLog");
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ERROR] Database read failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Error fetching disk status: {ex}");
        }

        return disks;
    }

    public async Task<List<TrendDataPoint>> GetTrendDataAsync(string driveName, int daysBack = 7)
    {
        var trends = new List<TrendDataPoint>();

        try
        {
            var dbPath = _connectionString.Replace("Data Source=", "").Split(';')[0];
            if (!File.Exists(dbPath))
            {
                return trends;
            }

            _logger?.Log($"[DB] Querying local Agent database: GetTrendData for {driveName}");

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

            _logger?.Log($"[DB] Retrieved {trends.Count} rows from DiskSpaceLog for trend data");
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ERROR] Database read failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Error fetching trend data: {ex}");
        }

        return trends;
    }

    public async Task<List<string>> GetMonitoredDrivesAsync()
    {
        var drives = new List<string>();

        try
        {
            var dbPath = _connectionString.Replace("Data Source=", "").Split(';')[0];
            if (!File.Exists(dbPath))
            {
                return drives;
            }

            _logger?.Log("[DB] Querying local Agent database: GetMonitoredDrives");

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

            _logger?.Log($"[DB] Retrieved {drives.Count} monitored drives");
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ERROR] Database read failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Error fetching monitored drives: {ex}");
        }

        return drives;
    }
}

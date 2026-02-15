using Microsoft.Data.Sqlite;
using StorageWatchUI.Models;
using StorageWatchUI.Communication;
using System.IO;

namespace StorageWatchUI.Services;

/// <summary>
/// Enhanced local data provider that uses IPC communication when possible,
/// with fallback to direct SQLite access with WAL mode for safe concurrent reads.
/// </summary>
public class EnhancedLocalDataProvider : IDataProvider
{
    private readonly string _connectionString;
    private readonly ServiceCommunicationClient _communicationClient;

    public EnhancedLocalDataProvider(string databasePath)
    {
        _connectionString = $"Data Source={databasePath};Mode=ReadOnly;";
        _communicationClient = new ServiceCommunicationClient();
    }

    public async Task<List<DiskInfo>> GetCurrentDiskStatusAsync()
    {
        // Try IPC first for better concurrency handling
        try
        {
            var query = new LocalDataQuery
            {
                QueryType = "RecentUsage",
                Limit = 50
            };

            var result = await _communicationClient.GetLocalDataAsync(query);
            if (result.HasValue)
            {
                // Parse result from IPC
                return ParseDiskInfoFromJson(result.Value);
            }
        }
        catch
        {
            // Fall through to direct SQLite access
        }

        // Fallback: Direct SQLite read
        return await GetCurrentDiskStatusFromDatabaseAsync();
    }

    public async Task<List<TrendDataPoint>> GetTrendDataAsync(string driveName, int daysBack = 7)
    {
        // Try IPC first
        try
        {
            var query = new LocalDataQuery
            {
                QueryType = "TrendData",
                DriveName = driveName,
                DaysBack = daysBack,
                Limit = 1000
            };

            var result = await _communicationClient.GetLocalDataAsync(query);
            if (result.HasValue)
            {
                return ParseTrendDataFromJson(result.Value);
            }
        }
        catch
        {
            // Fall through to direct SQLite access
        }

        // Fallback: Direct SQLite read
        return await GetTrendDataFromDatabaseAsync(driveName, daysBack);
    }

    public async Task<List<string>> GetMonitoredDrivesAsync()
    {
        var drives = new List<string>();

        try
        {
            var dbPath = _connectionString.Replace("Data Source=", "").Split(';')[0];
            if (!File.Exists(dbPath))
                return drives;

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

    private async Task<List<DiskInfo>> GetCurrentDiskStatusFromDatabaseAsync()
    {
        var disks = new List<DiskInfo>();

        try
        {
            var dbPath = _connectionString.Replace("Data Source=", "").Split(';')[0];
            if (!File.Exists(dbPath))
                return disks;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

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

    private async Task<List<TrendDataPoint>> GetTrendDataFromDatabaseAsync(string driveName, int daysBack)
    {
        var trends = new List<TrendDataPoint>();

        try
        {
            var dbPath = _connectionString.Replace("Data Source=", "").Split(';')[0];
            if (!File.Exists(dbPath))
                return trends;

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

    private List<DiskInfo> ParseDiskInfoFromJson(System.Text.Json.JsonElement data)
    {
        // Parse JSON response from IPC into DiskInfo objects
        var disks = new List<DiskInfo>();
        
        try
        {
            if (data.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var totalSpace = item.GetProperty("TotalSpaceGb").GetDouble();
                    var freeSpace = item.GetProperty("FreeSpaceGb").GetDouble();
                    var percentFree = totalSpace > 0 ? (freeSpace / totalSpace) * 100 : 0;

                    var disk = new DiskInfo
                    {
                        DriveName = item.GetProperty("DriveName").GetString() ?? "",
                        TotalSpaceGb = totalSpace,
                        FreeSpaceGb = freeSpace,
                        Status = DiskInfo.CalculateStatus(percentFree),
                        LastUpdated = item.GetProperty("LastUpdated").GetDateTime()
                    };
                    disks.Add(disk);
                }
            }
        }
        catch
        {
            // Return empty list on parse error
        }

        return disks;
    }

    private List<TrendDataPoint> ParseTrendDataFromJson(System.Text.Json.JsonElement data)
    {
        var trends = new List<TrendDataPoint>();
        
        try
        {
            if (data.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var point = new TrendDataPoint
                    {
                        Timestamp = item.GetProperty("Timestamp").GetDateTime(),
                        DriveName = item.GetProperty("DriveName").GetString() ?? "",
                        PercentFree = item.GetProperty("PercentFree").GetDouble(),
                        FreeSpaceGb = item.GetProperty("FreeSpaceGb").GetDouble(),
                        TotalSpaceGb = item.GetProperty("TotalSpaceGb").GetDouble()
                    };
                    trends.Add(point);
                }
            }
        }
        catch
        {
            // Return empty list on parse error
        }

        return trends;
    }
}

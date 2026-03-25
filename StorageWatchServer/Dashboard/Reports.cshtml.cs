using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Services;
using StorageWatchServer.Services.Logging;

namespace StorageWatchServer.Dashboard;

public class ReportsModel : PageModel
{
    private readonly ServerOptions _options;
    private readonly ILogger<ReportsModel> _logger;
    private readonly RollingFileLogger? _rollingLogger;
    private readonly ServerDatabaseShutdownCoordinator _databaseShutdownCoordinator;

    public int DefaultCount { get; } = 50;

    public List<MachineReportGroup> RecentReportsByMachine { get; set; } = new();

    public ReportsModel(
        ServerOptions options,
        ILogger<ReportsModel> logger,
        RollingFileLogger? rollingLogger = null,
        ServerDatabaseShutdownCoordinator? databaseShutdownCoordinator = null)
    {
        _options = options;
        _logger = logger;
        _rollingLogger = rollingLogger;
        _databaseShutdownCoordinator = databaseShutdownCoordinator ?? new ServerDatabaseShutdownCoordinator();
    }

    public async Task OnGetAsync()
    {
        try
        {
            await LoadRecentReportsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recent reports");
            _rollingLogger?.Log($"[WEBHOST] Razor page failed: {ex.Message}");
        }
    }

    private async Task LoadRecentReportsAsync()
    {
        await using var operation = await _databaseShutdownCoordinator.BeginOperationAsync();

        var connectionString = GetConnectionString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Get the most recent rows grouped by machine
        var sql = @"
            SELECT DISTINCT
                MachineName,
                MAX(Timestamp) as LatestTimestamp
            FROM RawDriveRows
            GROUP BY MachineName
            ORDER BY LatestTimestamp DESC
            LIMIT @count;
        ";

        var machineGroups = new List<(string MachineName, DateTime LatestTimestamp)>();

        await using (var command = new SqliteCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@count", DefaultCount);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var machineName = reader.GetString(0);
                var latestTimestamp = DateTime.Parse(reader.GetString(1));
                machineGroups.Add((machineName, latestTimestamp));
            }
        }

        // For each machine group, fetch its latest rows
        foreach (var (machineName, latestTimestamp) in machineGroups)
        {
            var rowsSql = @"
                SELECT DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree, Timestamp
                FROM RawDriveRows
                WHERE MachineName = @machineName AND Timestamp = @timestamp
                ORDER BY DriveLetter;
            ";

            var rows = new List<RawDriveRow>();

            await using (var rowsCommand = new SqliteCommand(rowsSql, connection))
            {
                rowsCommand.Parameters.AddWithValue("@machineName", machineName);
                rowsCommand.Parameters.AddWithValue("@timestamp", latestTimestamp);
                await using var reader = await rowsCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RawDriveRow
                    {
                        DriveLetter = reader.GetString(0),
                        TotalSpaceGb = reader.GetDouble(1),
                        UsedSpaceGb = reader.GetDouble(2),
                        FreeSpaceGb = reader.GetDouble(3),
                        PercentFree = reader.GetDouble(4),
                        Timestamp = DateTime.Parse(reader.GetString(5))
                    });
                }
            }

            RecentReportsByMachine.Add(new MachineReportGroup
            {
                MachineName = machineName,
                LatestTimestamp = latestTimestamp,
                Rows = rows
            });
        }
    }

    private string GetConnectionString()
    {
        if (_options.DatabasePath.Contains("mode=memory") || _options.DatabasePath.StartsWith("file:"))
        {
            return $"Data Source={_options.DatabasePath}";
        }

        var databasePath = Path.GetFullPath(_options.DatabasePath);
        return $"Data Source={databasePath}";
    }
}

public class MachineReportGroup
{
    public string MachineName { get; set; } = string.Empty;
    public DateTime LatestTimestamp { get; set; }
    public List<RawDriveRow> Rows { get; set; } = new();
}

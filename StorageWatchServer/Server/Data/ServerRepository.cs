using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Server.Data;

public class ServerRepository
{
    private readonly ServerOptions _options;

    public ServerRepository(ServerOptions options)
    {
        _options = options;
    }

    private string GetConnectionString()
    {
        // Handle in-memory database connection strings (for testing)
        if (_options.DatabasePath.Contains("mode=memory") || _options.DatabasePath.StartsWith("file:"))
        {
            return $"Data Source={_options.DatabasePath}";
        }
        
        var databasePath = Path.GetFullPath(_options.DatabasePath);
        return $"Data Source={databasePath}";
    }

    public async Task<int> UpsertMachineAsync(string machineName, DateTime lastSeenUtc)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var upsert = @"
            INSERT INTO Machines (MachineName, LastSeenUtc, CreatedUtc)
            VALUES (@name, @lastSeen, @created)
            ON CONFLICT(MachineName) DO UPDATE SET LastSeenUtc = excluded.LastSeenUtc;
        ";

        await using (var command = new SqliteCommand(upsert, connection))
        {
            command.Parameters.AddWithValue("@name", machineName);
            command.Parameters.AddWithValue("@lastSeen", lastSeenUtc);
            command.Parameters.AddWithValue("@created", lastSeenUtc);
            await command.ExecuteNonQueryAsync();
        }

        var select = "SELECT Id FROM Machines WHERE MachineName = @name";
        await using (var command = new SqliteCommand(select, connection))
        {
            command.Parameters.AddWithValue("@name", machineName);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }

    public async Task UpsertDriveAsync(int machineId, MachineDriveStatus drive)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var upsert = @"
            INSERT INTO MachineDrives
                (MachineId, DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree, LastSeenUtc)
            VALUES
                (@machineId, @driveLetter, @total, @used, @free, @percent, @lastSeen)
            ON CONFLICT(MachineId, DriveLetter) DO UPDATE SET
                TotalSpaceGb = excluded.TotalSpaceGb,
                UsedSpaceGb = excluded.UsedSpaceGb,
                FreeSpaceGb = excluded.FreeSpaceGb,
                PercentFree = excluded.PercentFree,
                LastSeenUtc = excluded.LastSeenUtc;
        ";

        await using var command = new SqliteCommand(upsert, connection);
        command.Parameters.AddWithValue("@machineId", machineId);
        command.Parameters.AddWithValue("@driveLetter", drive.DriveLetter);
        command.Parameters.AddWithValue("@total", drive.TotalSpaceGb);
        command.Parameters.AddWithValue("@used", drive.UsedSpaceGb);
        command.Parameters.AddWithValue("@free", drive.FreeSpaceGb);
        command.Parameters.AddWithValue("@percent", drive.PercentFree);
        command.Parameters.AddWithValue("@lastSeen", drive.LastSeenUtc);
        await command.ExecuteNonQueryAsync();
    }

    public async Task InsertDiskHistoryAsync(int machineId, string driveLetter, DiskHistoryPoint point)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var insert = @"
            INSERT INTO DiskHistory
                (MachineId, DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree, CollectionTimeUtc)
            VALUES
                (@machineId, @driveLetter, @total, @used, @free, @percent, @collectionTime);
        ";

        await using var command = new SqliteCommand(insert, connection);
        command.Parameters.AddWithValue("@machineId", machineId);
        command.Parameters.AddWithValue("@driveLetter", driveLetter);
        command.Parameters.AddWithValue("@total", point.TotalSpaceGb);
        command.Parameters.AddWithValue("@used", point.UsedSpaceGb);
        command.Parameters.AddWithValue("@free", point.FreeSpaceGb);
        command.Parameters.AddWithValue("@percent", point.PercentFree);
        command.Parameters.AddWithValue("@collectionTime", point.CollectionTimeUtc);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<MachineSummary>> GetMachinesAsync()
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var machineSql = "SELECT Id, MachineName, LastSeenUtc, CreatedUtc FROM Machines ORDER BY MachineName";
        var machines = new List<MachineSummary>();

        await using (var command = new SqliteCommand(machineSql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                machines.Add(new MachineSummary
                {
                    Id = reader.GetInt32(0),
                    MachineName = reader.GetString(1),
                    LastSeenUtc = reader.GetDateTime(2)
                });
            }
        }

        foreach (var machine in machines)
        {
            machine.Drives = await GetMachineDrivesAsync(connection, machine.Id);
        }

        return machines;
    }

    public async Task<MachineDetails?> GetMachineAsync(int machineId)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var machineSql = "SELECT Id, MachineName, LastSeenUtc, CreatedUtc FROM Machines WHERE Id = @id";
        await using var command = new SqliteCommand(machineSql, connection);
        command.Parameters.AddWithValue("@id", machineId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var machine = new MachineDetails
        {
            Id = reader.GetInt32(0),
            MachineName = reader.GetString(1),
            LastSeenUtc = reader.GetDateTime(2),
            CreatedUtc = reader.GetDateTime(3)
        };

        machine.Drives = await GetMachineDrivesAsync(connection, machine.Id);
        return machine;
    }

    public async Task<IReadOnlyList<MachineDriveStatus>> GetMachineDrivesAsync(int machineId)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();
        return await GetMachineDrivesAsync(connection, machineId);
    }

    private static async Task<IReadOnlyList<MachineDriveStatus>> GetMachineDrivesAsync(SqliteConnection connection, int machineId)
    {
        var driveSql = @"
            SELECT DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree, LastSeenUtc
            FROM MachineDrives
            WHERE MachineId = @machineId
            ORDER BY DriveLetter;
        ";

        var drives = new List<MachineDriveStatus>();
        await using var driveCommand = new SqliteCommand(driveSql, connection);
        driveCommand.Parameters.AddWithValue("@machineId", machineId);

        await using var driveReader = await driveCommand.ExecuteReaderAsync();
        while (await driveReader.ReadAsync())
        {
            drives.Add(new MachineDriveStatus
            {
                DriveLetter = driveReader.GetString(0),
                TotalSpaceGb = driveReader.GetDouble(1),
                UsedSpaceGb = driveReader.GetDouble(2),
                FreeSpaceGb = driveReader.GetDouble(3),
                PercentFree = driveReader.GetDouble(4),
                LastSeenUtc = driveReader.GetDateTime(5)
            });
        }

        return drives;
    }

    public async Task<IReadOnlyList<DiskHistoryPoint>> GetDiskHistoryAsync(int machineId, string driveLetter, DateTime startUtc)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var historySql = @"
            SELECT CollectionTimeUtc, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree
            FROM DiskHistory
            WHERE MachineId = @machineId AND DriveLetter = @driveLetter AND CollectionTimeUtc >= @startUtc
            ORDER BY CollectionTimeUtc;
        ";

        var points = new List<DiskHistoryPoint>();
        await using var command = new SqliteCommand(historySql, connection);
        command.Parameters.AddWithValue("@machineId", machineId);
        command.Parameters.AddWithValue("@driveLetter", driveLetter);
        command.Parameters.AddWithValue("@startUtc", startUtc);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            points.Add(new DiskHistoryPoint
            {
                CollectionTimeUtc = reader.GetDateTime(0),
                TotalSpaceGb = reader.GetDouble(1),
                UsedSpaceGb = reader.GetDouble(2),
                FreeSpaceGb = reader.GetDouble(3),
                PercentFree = reader.GetDouble(4)
            });
        }

        return points;
    }

    public async Task<IReadOnlyList<AlertRecord>> GetAlertsAsync()
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var alertSql = @"
            SELECT Alerts.Id, Alerts.MachineId, Machines.MachineName, Alerts.Severity, Alerts.Message,
                   Alerts.CreatedUtc, Alerts.ResolvedUtc, Alerts.IsActive
            FROM Alerts
            INNER JOIN Machines ON Machines.Id = Alerts.MachineId
            ORDER BY Alerts.IsActive DESC, Alerts.CreatedUtc DESC;
        ";

        var alerts = new List<AlertRecord>();
        await using var command = new SqliteCommand(alertSql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            alerts.Add(new AlertRecord
            {
                Id = reader.GetInt32(0),
                MachineId = reader.GetInt32(1),
                MachineName = reader.GetString(2),
                Severity = reader.GetString(3),
                Message = reader.GetString(4),
                CreatedUtc = reader.GetDateTime(5),
                ResolvedUtc = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                IsActive = reader.GetInt32(7) == 1
            });
        }

        return alerts;
    }

    public async Task<IReadOnlyList<SettingRecord>> GetSettingsAsync()
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var settingsSql = "SELECT Key, Value, Description FROM Settings ORDER BY Key";
        var settings = new List<SettingRecord>();

        await using var command = new SqliteCommand(settingsSql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            settings.Add(new SettingRecord
            {
                Key = reader.GetString(0),
                Value = reader.GetString(1),
                Description = reader.GetString(2)
            });
        }

        return settings;
    }
}

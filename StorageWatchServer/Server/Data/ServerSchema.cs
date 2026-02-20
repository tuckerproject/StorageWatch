using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Server.Data;

public class ServerSchema
{
    private readonly ServerOptions _options;

    public ServerSchema(ServerOptions options)
    {
        _options = options;
    }

    public async Task InitializeDatabaseAsync()
    {
        var databasePath = Path.GetFullPath(_options.DatabasePath);
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = $"Data Source={databasePath}";

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var createMachines = @"
            CREATE TABLE IF NOT EXISTS Machines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineName TEXT NOT NULL UNIQUE,
                LastSeenUtc DATETIME NOT NULL,
                CreatedUtc DATETIME NOT NULL
            );
        ";

        var createMachineDrives = @"
            CREATE TABLE IF NOT EXISTS MachineDrives (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineId INTEGER NOT NULL,
                DriveLetter TEXT NOT NULL,
                TotalSpaceGb REAL NOT NULL,
                UsedSpaceGb REAL NOT NULL,
                FreeSpaceGb REAL NOT NULL,
                PercentFree REAL NOT NULL,
                LastSeenUtc DATETIME NOT NULL,
                UNIQUE(MachineId, DriveLetter)
            );
        ";

        var createDiskHistory = @"
            CREATE TABLE IF NOT EXISTS DiskHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineId INTEGER NOT NULL,
                DriveLetter TEXT NOT NULL,
                TotalSpaceGb REAL NOT NULL,
                UsedSpaceGb REAL NOT NULL,
                FreeSpaceGb REAL NOT NULL,
                PercentFree REAL NOT NULL,
                CollectionTimeUtc DATETIME NOT NULL
            );
        ";

        var createAlerts = @"
            CREATE TABLE IF NOT EXISTS Alerts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineId INTEGER NOT NULL,
                Severity TEXT NOT NULL,
                Message TEXT NOT NULL,
                CreatedUtc DATETIME NOT NULL,
                ResolvedUtc DATETIME NULL,
                IsActive INTEGER NOT NULL
            );
        ";

        var createSettings = @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                Description TEXT NOT NULL
            );
        ";

        await using (var command = new SqliteCommand(createMachines, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new SqliteCommand(createMachineDrives, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new SqliteCommand(createDiskHistory, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new SqliteCommand(createAlerts, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new SqliteCommand(createSettings, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var createHistoryIndex = @"
            CREATE INDEX IF NOT EXISTS idx_DiskHistory_Machine_Drive_Time
            ON DiskHistory(MachineId, DriveLetter, CollectionTimeUtc);
        ";

        await using (var command = new SqliteCommand(createHistoryIndex, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var createMachineDriveIndex = @"
            CREATE INDEX IF NOT EXISTS idx_MachineDrives_Machine
            ON MachineDrives(MachineId, DriveLetter);
        ";

        await using (var command = new SqliteCommand(createMachineDriveIndex, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var seedSettings = @"
            INSERT INTO Settings (Key, Value, Description)
            VALUES
            ('OnlineTimeoutMinutes', @timeout, 'Minutes before a machine is considered offline.'),
            ('ListenUrl', @listenUrl, 'The URL Kestrel listens on for dashboard traffic.'),
            ('DatabasePath', @databasePath, 'The SQLite database location for the central dashboard.')
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
        ";

        await using (var command = new SqliteCommand(seedSettings, connection))
        {
            command.Parameters.AddWithValue("@timeout", _options.OnlineTimeoutMinutes);
            command.Parameters.AddWithValue("@listenUrl", _options.ListenUrl);
            command.Parameters.AddWithValue("@databasePath", _options.DatabasePath);
            await command.ExecuteNonQueryAsync();
        }
    }
}
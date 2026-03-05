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
        string connectionString;
        if (_options.DatabasePath.Contains("mode=memory") || _options.DatabasePath.StartsWith("file:"))
        {
            connectionString = $"Data Source={_options.DatabasePath}";
        }
        else
        {
            var databasePath = Path.GetFullPath(_options.DatabasePath);
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            connectionString = $"Data Source={databasePath}";
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Enable foreign keys
        await using (var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Create RawDriveRows table - stores exactly what Agents send
        var createRawDriveRows = @"
            CREATE TABLE IF NOT EXISTS RawDriveRows (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineName TEXT NOT NULL,
                DriveLetter TEXT NOT NULL,
                TotalSpaceGb REAL NOT NULL,
                UsedSpaceGb REAL NOT NULL,
                FreeSpaceGb REAL NOT NULL,
                PercentFree REAL NOT NULL,
                Timestamp DATETIME NOT NULL
            );
        ";

        await using (var command = new SqliteCommand(createRawDriveRows, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Create index for efficient queries by machine name and timestamp
        var createRawDriveRowsIndex = @"
            CREATE INDEX IF NOT EXISTS idx_RawDriveRows_Machine_Time
            ON RawDriveRows(MachineName, Timestamp DESC);
        ";

        await using (var command = new SqliteCommand(createRawDriveRowsIndex, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Create Machines table for tracking unique machines
        var createMachines = @"
            CREATE TABLE IF NOT EXISTS Machines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineName TEXT NOT NULL UNIQUE,
                LastSeenUtc DATETIME NOT NULL,
                CreatedUtc DATETIME NOT NULL
            );
        ";

        await using (var command = new SqliteCommand(createMachines, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Create Settings table for configuration
        var createSettings = @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                Description TEXT NOT NULL
            );
        ";

        await using (var command = new SqliteCommand(createSettings, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var seedSettings = @"
            INSERT INTO Settings (Key, Value, Description)
            VALUES
            ('OnlineTimeoutMinutes', @timeout, 'Minutes before a machine is considered offline.'),
            ('ListenUrl', @listenUrl, 'The URL Kestrel listens on for dashboard traffic.')
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
        ";

        await using (var command = new SqliteCommand(seedSettings, connection))
        {
            command.Parameters.AddWithValue("@timeout", _options.OnlineTimeoutMinutes);
            command.Parameters.AddWithValue("@listenUrl", _options.ListenUrl);
            await command.ExecuteNonQueryAsync();
        }
    }
}
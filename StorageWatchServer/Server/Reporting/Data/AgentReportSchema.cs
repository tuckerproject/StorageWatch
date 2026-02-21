using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Server.Reporting.Data;

public class AgentReportSchema
{
    private readonly ServerOptions _options;

    public AgentReportSchema(ServerOptions options)
    {
        _options = options;
    }

    public async Task InitializeDatabaseAsync()
    {
        string connectionString;
        if (_options.AgentReportDatabasePath.Contains("mode=memory") || _options.AgentReportDatabasePath.StartsWith("file:"))
        {
            connectionString = $"Data Source={_options.AgentReportDatabasePath}";
        }
        else
        {
            var databasePath = Path.GetFullPath(_options.AgentReportDatabasePath);
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            connectionString = $"Data Source={databasePath}";
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using (var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var createAgents = @"
            CREATE TABLE IF NOT EXISTS Agents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AgentId TEXT NOT NULL UNIQUE,
                LastSeenUtc DATETIME NOT NULL,
                CreatedUtc DATETIME NOT NULL
            );
        ";

        var createDriveReports = @"
            CREATE TABLE IF NOT EXISTS DriveReports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AgentId INTEGER NOT NULL,
                DriveLetter TEXT NOT NULL,
                TotalSpaceGb REAL NOT NULL,
                FreeSpaceGb REAL NOT NULL,
                UsedPercent REAL NOT NULL,
                TimestampUtc DATETIME NOT NULL,
                FOREIGN KEY (AgentId) REFERENCES Agents(Id)
            );
        ";

        var createAlerts = @"
            CREATE TABLE IF NOT EXISTS Alerts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AgentId INTEGER NOT NULL,
                DriveLetter TEXT NOT NULL,
                Level TEXT NOT NULL,
                Message TEXT NOT NULL,
                TimestampUtc DATETIME NOT NULL,
                FOREIGN KEY (AgentId) REFERENCES Agents(Id)
            );
        ";

        await using (var command = new SqliteCommand(createAgents, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new SqliteCommand(createDriveReports, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new SqliteCommand(createAlerts, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var createDriveReportsIndex = @"
            CREATE INDEX IF NOT EXISTS idx_DriveReports_Agent_Time
            ON DriveReports(AgentId, TimestampUtc);
        ";

        await using (var command = new SqliteCommand(createDriveReportsIndex, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var createAlertsIndex = @"
            CREATE INDEX IF NOT EXISTS idx_Alerts_Agent_Time
            ON Alerts(AgentId, TimestampUtc);
        ";

        await using (var command = new SqliteCommand(createAlertsIndex, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var createAgentsIndex = @"
            CREATE INDEX IF NOT EXISTS idx_Agents_AgentId
            ON Agents(AgentId);
        ";

        await using (var command = new SqliteCommand(createAgentsIndex, connection))
        {
            await command.ExecuteNonQueryAsync();
        }
    }
}

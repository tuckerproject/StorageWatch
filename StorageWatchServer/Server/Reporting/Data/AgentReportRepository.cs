using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Reporting.Models;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Server.Reporting.Data;

public class AgentReportRepository : IAgentReportRepository
{
    private readonly ServerOptions _options;

    public AgentReportRepository(ServerOptions options)
    {
        _options = options;
    }

    public async Task SaveReportAsync(AgentReport report)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();
        await EnableForeignKeysAsync(connection);

        using var transaction = connection.BeginTransaction();

        // Use the AgentReport's TimestampUtc as the canonical timestamp for all records
        var reportTimestamp = ToSqliteDateTime(report.TimestampUtc);

        var upsertAgent = @"
            INSERT INTO Agents (AgentId, LastSeenUtc, CreatedUtc)
            VALUES (@agentId, @lastSeenUtc, @createdUtc)
            ON CONFLICT(AgentId) DO UPDATE SET LastSeenUtc = excluded.LastSeenUtc;
        ";

        await using (var command = new SqliteCommand(upsertAgent, connection))
        {
            command.Transaction = transaction;
            command.Parameters.AddWithValue("@agentId", report.AgentId);
            command.Parameters.AddWithValue("@lastSeenUtc", reportTimestamp);
            command.Parameters.AddWithValue("@createdUtc", reportTimestamp);
            await command.ExecuteNonQueryAsync();
        }

        var selectAgentId = "SELECT Id FROM Agents WHERE AgentId = @agentId";
        long agentKey;
        await using (var command = new SqliteCommand(selectAgentId, connection))
        {
            command.Transaction = transaction;
            command.Parameters.AddWithValue("@agentId", report.AgentId);
            var result = await command.ExecuteScalarAsync();
            agentKey = Convert.ToInt64(result);
        }

        var insertDrive = @"
            INSERT INTO DriveReports
                (AgentId, DriveLetter, TotalSpaceGb, FreeSpaceGb, UsedPercent, TimestampUtc)
            VALUES
                (@agentId, @driveLetter, @totalSpaceGb, @freeSpaceGb, @usedPercent, @timestampUtc);
        ";

        foreach (var drive in report.Drives)
        {
            await using var driveCommand = new SqliteCommand(insertDrive, connection);
            driveCommand.Transaction = transaction;
            driveCommand.Parameters.AddWithValue("@agentId", agentKey);
            driveCommand.Parameters.AddWithValue("@driveLetter", drive.DriveLetter);
            driveCommand.Parameters.AddWithValue("@totalSpaceGb", drive.TotalSpaceGb);
            driveCommand.Parameters.AddWithValue("@freeSpaceGb", drive.FreeSpaceGb);
            driveCommand.Parameters.AddWithValue("@usedPercent", drive.UsedPercent);
            driveCommand.Parameters.AddWithValue("@timestampUtc", reportTimestamp);
            await driveCommand.ExecuteNonQueryAsync();
        }

        var insertAlert = @"
            INSERT INTO Alerts
                (AgentId, DriveLetter, Level, Message, TimestampUtc)
            VALUES
                (@agentId, @driveLetter, @level, @message, @timestampUtc);
        ";

        foreach (var alert in report.Alerts)
        {
            await using var alertCommand = new SqliteCommand(insertAlert, connection);
            alertCommand.Transaction = transaction;
            alertCommand.Parameters.AddWithValue("@agentId", agentKey);
            alertCommand.Parameters.AddWithValue("@driveLetter", alert.DriveLetter);
            alertCommand.Parameters.AddWithValue("@level", alert.Level);
            alertCommand.Parameters.AddWithValue("@message", alert.Message);
            alertCommand.Parameters.AddWithValue("@timestampUtc", reportTimestamp);
            await alertCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<AgentReport>> GetRecentReportsAsync(int count)
    {
        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();
        await EnableForeignKeysAsync(connection);

        var reportKeysSql = @"
            SELECT Agents.AgentId, DriveReports.TimestampUtc
            FROM DriveReports
            INNER JOIN Agents ON Agents.Id = DriveReports.AgentId
            GROUP BY Agents.AgentId, DriveReports.TimestampUtc
            ORDER BY DriveReports.TimestampUtc DESC
            LIMIT @count;
        ";

        var reportKeys = new List<(string AgentId, string TimestampUtc)>();
        await using (var command = new SqliteCommand(reportKeysSql, connection))
        {
            command.Parameters.AddWithValue("@count", count);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                reportKeys.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var reports = new List<AgentReport>();
        foreach (var key in reportKeys)
        {
            var report = new AgentReport
            {
                AgentId = key.AgentId,
                TimestampUtc = FromSqliteDateTime(key.TimestampUtc)
            };

            var driveSql = @"
                SELECT DriveReports.DriveLetter, DriveReports.TotalSpaceGb, DriveReports.FreeSpaceGb,
                       DriveReports.UsedPercent, DriveReports.TimestampUtc
                FROM DriveReports
                INNER JOIN Agents ON Agents.Id = DriveReports.AgentId
                WHERE Agents.AgentId = @agentId AND DriveReports.TimestampUtc = @timestampUtc
                ORDER BY DriveReports.DriveLetter;
            ";

            await using (var driveCommand = new SqliteCommand(driveSql, connection))
            {
                driveCommand.Parameters.AddWithValue("@agentId", key.AgentId);
                driveCommand.Parameters.AddWithValue("@timestampUtc", key.TimestampUtc);
                await using var driveReader = await driveCommand.ExecuteReaderAsync();
                while (await driveReader.ReadAsync())
                {
                    report.Drives.Add(new DriveReport
                    {
                        DriveLetter = driveReader.GetString(0),
                        TotalSpaceGb = driveReader.GetDouble(1),
                        FreeSpaceGb = driveReader.GetDouble(2),
                        UsedPercent = driveReader.GetDouble(3),
                        TimestampUtc = FromSqliteDateTime(driveReader.GetString(4))
                    });
                }
            }

            var alertSql = @"
                SELECT Alerts.DriveLetter, Alerts.Level, Alerts.Message, Alerts.TimestampUtc
                FROM Alerts
                INNER JOIN Agents ON Agents.Id = Alerts.AgentId
                WHERE Agents.AgentId = @agentId AND Alerts.TimestampUtc = @timestampUtc
                ORDER BY Alerts.Id;
            ";

            await using (var alertCommand = new SqliteCommand(alertSql, connection))
            {
                alertCommand.Parameters.AddWithValue("@agentId", key.AgentId);
                alertCommand.Parameters.AddWithValue("@timestampUtc", key.TimestampUtc);
                await using var alertReader = await alertCommand.ExecuteReaderAsync();
                while (await alertReader.ReadAsync())
                {
                    report.Alerts.Add(new AlertRecord
                    {
                        DriveLetter = alertReader.GetString(0),
                        Level = alertReader.GetString(1),
                        Message = alertReader.GetString(2),
                        TimestampUtc = FromSqliteDateTime(alertReader.GetString(3))
                    });
                }
            }

            reports.Add(report);
        }

        return reports;
    }

    private string GetConnectionString()
    {
        if (_options.AgentReportDatabasePath.Contains("mode=memory") || _options.AgentReportDatabasePath.StartsWith("file:"))
        {
            return $"Data Source={_options.AgentReportDatabasePath}";
        }

        var databasePath = Path.GetFullPath(_options.AgentReportDatabasePath);
        return $"Data Source={databasePath}";
    }

    private static async Task EnableForeignKeysAsync(SqliteConnection connection)
    {
        await using var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string ToSqliteDateTime(DateTime dateTime)
    {
        // Store as ticks to preserve full precision
        return dateTime.Ticks.ToString();
    }

    private static DateTime FromSqliteDateTime(string sqliteDateTime)
    {
        // Parse ticks back to DateTime
        var ticks = long.Parse(sqliteDateTime);
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}

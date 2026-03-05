using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Server.Reporting;

/// <summary>
/// Service for inserting raw drive rows into the database exactly as received.
/// Performs no transformations, normalization, or deduplication.
/// </summary>
public class RawRowIngestionService
{
    private readonly ServerOptions _options;

    public RawRowIngestionService(ServerOptions options)
    {
        _options = options;
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

    /// <summary>
    /// Inserts a batch of raw drive rows into the database.
    /// </summary>
    public async Task IngestRawRowsAsync(string machineName, List<RawDriveRow> rows)
    {
        if (string.IsNullOrWhiteSpace(machineName) || rows == null || rows.Count == 0)
        {
            throw new ArgumentException("Machine name must be provided and rows list must not be empty.");
        }

        await using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        await using (var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO RawDriveRows
                (MachineName, DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree, Timestamp)
            VALUES
                (@machineName, @driveLetter, @totalSpaceGb, @usedSpaceGb, @freeSpaceGb, @percentFree, @timestamp);
        ";

        foreach (var row in rows)
        {
            await using var command = new SqliteCommand(insertSql, connection);
            command.Transaction = transaction;
            command.Parameters.AddWithValue("@machineName", machineName);
            command.Parameters.AddWithValue("@driveLetter", row.DriveLetter ?? string.Empty);
            command.Parameters.AddWithValue("@totalSpaceGb", row.TotalSpaceGb);
            command.Parameters.AddWithValue("@usedSpaceGb", row.UsedSpaceGb);
            command.Parameters.AddWithValue("@freeSpaceGb", row.FreeSpaceGb);
            command.Parameters.AddWithValue("@percentFree", row.PercentFree);
            command.Parameters.AddWithValue("@timestamp", row.Timestamp);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
}

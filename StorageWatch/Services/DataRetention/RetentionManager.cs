/// <summary>
/// Data Retention Manager Service
/// 
/// Manages automatic cleanup of old disk space log entries from SQLite.
/// Supports deletion by age (MaxDays), by row count (MaxRows), and optional archiving to CSV.
/// Cleanup operations are non-blocking and logged for audit purposes.
/// </summary>

using Microsoft.Data.Sqlite;
using StorageWatch.Config.Options;
using StorageWatch.Services.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace StorageWatch.Services.DataRetention
{
    /// <summary>
    /// Manages data retention policies and cleanup operations for the DiskSpaceLog table.
    /// </summary>
    public class RetentionManager
    {
        private readonly string _connectionString;
        private readonly RetentionOptions _options;
        private readonly RollingFileLogger _logger;
        private DateTime _lastCleanupTime = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the RetentionManager class.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string.</param>
        /// <param name="options">The retention configuration options.</param>
        /// <param name="logger">The logger for recording cleanup operations.</param>
        public RetentionManager(string connectionString, RetentionOptions options, RollingFileLogger logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs cleanup if the interval since the last cleanup has elapsed.
        /// This method is safe to call frequently and will only perform cleanup at the configured interval.
        /// </summary>
        /// <returns>True if cleanup was performed; false if skipped due to interval.</returns>
        public async Task<bool> CheckAndCleanupAsync()
        {
            // Check if cleanup is enabled
            if (!_options.Enabled)
            {
                return false;
            }

            // Check if cleanup interval has elapsed
            var timeSinceLastCleanup = DateTime.UtcNow - _lastCleanupTime;
            var cleanupInterval = TimeSpan.FromMinutes(_options.CleanupIntervalMinutes);

            if (timeSinceLastCleanup < cleanupInterval)
            {
                return false;
            }

            // Perform cleanup
            try
            {
                await PerformCleanupAsync();
                _lastCleanupTime = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log($"[Retention ERROR] Cleanup operation failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Performs the actual cleanup operation: archives (if enabled) and deletes old data.
        /// </summary>
        private async Task PerformCleanupAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var deleteCount = 0;

                // Step 1: Archive old rows if archiving is enabled
                if (_options.ArchiveEnabled && _options.ExportCsvEnabled)
                {
                    deleteCount = await ArchiveAndDeleteOldRowsAsync(connection);
                }
                else
                {
                    // Step 2: Delete old rows by date (MaxDays)
                    deleteCount += await DeleteOldRowsByDateAsync(connection);
                }

                // Step 3: Trim by row count if MaxRows is set
                if (_options.MaxRows > 0)
                {
                    deleteCount += await TrimByRowCountAsync(connection);
                }

                if (deleteCount > 0)
                {
                    _logger.Log($"[Retention] Cleanup completed. Deleted {deleteCount} row(s).");
                }
                else
                {
                    _logger.Log("[Retention] Cleanup completed. No rows met deletion criteria.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[Retention ERROR] Failed to perform cleanup: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Archives old rows to a CSV file and then deletes them.
        /// </summary>
        /// <returns>Number of rows deleted.</returns>
        private async Task<int> ArchiveAndDeleteOldRowsAsync(SqliteConnection connection)
        {
            try
            {
                // Validate archive directory
                if (string.IsNullOrWhiteSpace(_options.ArchiveDirectory))
                {
                    _logger.Log("[Retention WARNING] ArchiveEnabled but ArchiveDirectory is empty. Skipping archive.");
                    return await DeleteOldRowsByDateAsync(connection);
                }

                // Ensure archive directory exists
                Directory.CreateDirectory(_options.ArchiveDirectory);

                // Get rows to be deleted (older than MaxDays)
                var cutoffDate = DateTime.UtcNow.AddDays(-_options.MaxDays);
                var rowsToArchive = new List<DiskSpaceLogEntry>();

                string selectSql = @"
                    SELECT Id, MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc, CreatedAt
                    FROM DiskSpaceLog
                    WHERE CollectionTimeUtc < @cutoff
                    ORDER BY CollectionTimeUtc ASC
                ";

                using var selectCommand = new SqliteCommand(selectSql, connection);
                selectCommand.Parameters.AddWithValue("@cutoff", cutoffDate);

                using var reader = await selectCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rowsToArchive.Add(new DiskSpaceLogEntry
                    {
                        Id = reader.GetInt32(0),
                        MachineName = reader.GetString(1),
                        DriveLetter = reader.GetString(2),
                        TotalSpaceGB = reader.GetDouble(3),
                        UsedSpaceGB = reader.GetDouble(4),
                        FreeSpaceGB = reader.GetDouble(5),
                        PercentFree = reader.GetDouble(6),
                        CollectionTimeUtc = reader.GetDateTime(7),
                        CreatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                    });
                }

                if (rowsToArchive.Count == 0)
                {
                    _logger.Log("[Retention] No rows to archive. Cleanup skipped.");
                    return 0;
                }

                // Export to CSV file
                string archiveFilePath = Path.Combine(
                    _options.ArchiveDirectory,
                    $"DiskSpaceLog_Archive_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
                );

                await ExportToCsvAsync(archiveFilePath, rowsToArchive);
                _logger.Log($"[Retention] Archived {rowsToArchive.Count} row(s) to {archiveFilePath}");

                // Delete the archived rows
                var deleteCount = await DeleteRowsByIdAsync(connection, rowsToArchive);
                _logger.Log($"[Retention] Deleted {deleteCount} archived row(s) from database.");

                return deleteCount;
            }
            catch (Exception ex)
            {
                _logger.Log($"[Retention ERROR] Archive and delete operation failed: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Deletes rows older than MaxDays without archiving.
        /// </summary>
        /// <returns>Number of rows deleted.</returns>
        private async Task<int> DeleteOldRowsByDateAsync(SqliteConnection connection)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-_options.MaxDays);

                string deleteSql = @"
                    DELETE FROM DiskSpaceLog
                    WHERE CollectionTimeUtc < @cutoff
                ";

                using var deleteCommand = new SqliteCommand(deleteSql, connection);
                deleteCommand.Parameters.AddWithValue("@cutoff", cutoffDate);

                var rowsAffected = await deleteCommand.ExecuteNonQueryAsync();
                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.Log($"[Retention ERROR] Failed to delete old rows by date: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Trims the table to MaxRows by deleting the oldest entries.
        /// </summary>
        /// <returns>Number of rows deleted.</returns>
        private async Task<int> TrimByRowCountAsync(SqliteConnection connection)
        {
            try
            {
                // Get the current row count
                using var countCommand = new SqliteCommand("SELECT COUNT(*) FROM DiskSpaceLog", connection);
                var count = (long)await countCommand.ExecuteScalarAsync();

                if (count <= _options.MaxRows)
                {
                    _logger.Log($"[Retention] Row count ({count}) is within limit ({_options.MaxRows}). No trimming needed.");
                    return 0;
                }

                // Calculate how many rows to delete
                var rowsToDelete = count - _options.MaxRows;

                // Delete the oldest rows
                string deleteSql = @"
                    DELETE FROM DiskSpaceLog
                    WHERE Id IN (
                        SELECT Id FROM DiskSpaceLog
                        ORDER BY CollectionTimeUtc ASC
                        LIMIT @limit
                    )
                ";

                using var deleteCommand = new SqliteCommand(deleteSql, connection);
                deleteCommand.Parameters.AddWithValue("@limit", (long)rowsToDelete);

                var deletedCount = await deleteCommand.ExecuteNonQueryAsync();
                _logger.Log($"[Retention] Trimmed {deletedCount} row(s) to respect MaxRows limit of {_options.MaxRows}.");

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.Log($"[Retention ERROR] Failed to trim by row count: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Exports disk space log entries to a CSV file.
        /// </summary>
        private async Task ExportToCsvAsync(string filePath, List<DiskSpaceLogEntry> entries)
        {
            try
            {
                using var writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None), Encoding.UTF8);

                // Write CSV header
                await writer.WriteLineAsync("Id,MachineName,DriveLetter,TotalSpaceGB,UsedSpaceGB,FreeSpaceGB,PercentFree,CollectionTimeUtc,CreatedAt");

                // Write data rows
                foreach (var entry in entries)
                {
                    var line = $"{entry.Id},\"{entry.MachineName}\",\"{entry.DriveLetter}\"," +
                               $"{entry.TotalSpaceGB.ToString(CultureInfo.InvariantCulture)}," +
                               $"{entry.UsedSpaceGB.ToString(CultureInfo.InvariantCulture)}," +
                               $"{entry.FreeSpaceGB.ToString(CultureInfo.InvariantCulture)}," +
                               $"{entry.PercentFree.ToString(CultureInfo.InvariantCulture)}," +
                               $"\"{entry.CollectionTimeUtc:O}\"," +
                               $"\"{(entry.CreatedAt?.ToString("O") ?? "")}\"";
                    await writer.WriteLineAsync(line);
                }

                await writer.FlushAsync();
                _logger.Log($"[Retention] Exported {entries.Count} row(s) to CSV: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Log($"[Retention ERROR] Failed to export to CSV: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Deletes specific rows by their IDs.
        /// </summary>
        /// <returns>Number of rows deleted.</returns>
        private async Task<int> DeleteRowsByIdAsync(SqliteConnection connection, List<DiskSpaceLogEntry> entries)
        {
            try
            {
                if (entries.Count == 0)
                    return 0;

                // Build a comma-separated list of IDs
                var ids = string.Join(",", entries.ConvertAll(e => e.Id));

                string deleteSql = $"DELETE FROM DiskSpaceLog WHERE Id IN ({ids})";

                using var deleteCommand = new SqliteCommand(deleteSql, connection);
                var rowsAffected = await deleteCommand.ExecuteNonQueryAsync();

                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.Log($"[Retention ERROR] Failed to delete rows by ID: {ex}");
                throw;
            }
        }
    }

    /// <summary>
    /// Represents a disk space log entry for archiving purposes.
    /// </summary>
    internal class DiskSpaceLogEntry
    {
        public int Id { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        public double TotalSpaceGB { get; set; }
        public double UsedSpaceGB { get; set; }
        public double FreeSpaceGB { get; set; }
        public double PercentFree { get; set; }
        public DateTime CollectionTimeUtc { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

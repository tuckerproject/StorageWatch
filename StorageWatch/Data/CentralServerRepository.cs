/// <summary>
/// Central Server Data Repository
/// 
/// This class provides data access methods for storing and retrieving disk space data
/// from the central database. It handles INSERT operations for inbound agent data
/// and provides query methods for reporting and analytics.
/// </summary>

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using StorageWatch.Services.Logging;

namespace StorageWatch.Data
{
    /// <summary>
    /// Data access layer for the central server database.
    /// Handles insertion of disk space data received from agents.
    /// </summary>
    public class CentralServerRepository
    {
        private readonly string _connectionString;
        private readonly RollingFileLogger _logger;

        /// <summary>
        /// Initializes a new instance of the CentralServerRepository class.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string for the central database.</param>
        /// <param name="logger">The logger for recording repository operations.</param>
        public CentralServerRepository(string connectionString, RollingFileLogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Inserts a disk space log entry into the central database.
        /// This is called when an agent submits its disk space data to the central server.
        /// </summary>
        /// <param name="agentMachineName">The name of the machine (agent) submitting the data.</param>
        /// <param name="driveLetter">The drive letter being reported (e.g., "C:").</param>
        /// <param name="totalSpaceGb">The total space on the drive in GB.</param>
        /// <param name="usedSpaceGb">The used space on the drive in GB.</param>
        /// <param name="freeSpaceGb">The free space on the drive in GB.</param>
        /// <param name="percentFree">The percentage of free space (0-100).</param>
        /// <param name="collectionTimeUtc">The UTC timestamp when the data was collected by the agent.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InsertDiskSpaceLogAsync(
            string agentMachineName,
            string driveLetter,
            double totalSpaceGb,
            double usedSpaceGb,
            double freeSpaceGb,
            double percentFree,
            DateTime collectionTimeUtc)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    INSERT INTO CentralDiskSpaceLog
                    (AgentMachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc)
                    VALUES (@agentMachine, @drive, @total, @used, @free, @percent, @collectionTime)
                ";

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@agentMachine", agentMachineName);
                command.Parameters.AddWithValue("@drive", driveLetter);
                command.Parameters.AddWithValue("@total", totalSpaceGb);
                command.Parameters.AddWithValue("@used", usedSpaceGb);
                command.Parameters.AddWithValue("@free", freeSpaceGb);
                command.Parameters.AddWithValue("@percent", percentFree);
                command.Parameters.AddWithValue("@collectionTime", collectionTimeUtc);

                await command.ExecuteNonQueryAsync();
                _logger.Log($"[CentralServer] Inserted disk space log: Agent={agentMachineName}, Drive={driveLetter}, Free={percentFree:F2}%");
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer ERROR] Failed to insert disk space log: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Gets the most recent disk space entries for all agents.
        /// Used for dashboard/status views showing current state of all monitored machines.
        /// </summary>
        /// <returns>A CSV string representation of the latest entries, or empty string if none exist.</returns>
        public async Task<string> GetLatestEntriesAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = @"
                    SELECT 
                        AgentMachineName, 
                        DriveLetter, 
                        TotalSpaceGB, 
                        UsedSpaceGB, 
                        FreeSpaceGB, 
                        PercentFree, 
                        CollectionTimeUtc,
                        ReceivedAtUtc
                    FROM CentralDiskSpaceLog
                    WHERE (AgentMachineName, DriveLetter, CollectionTimeUtc) IN (
                        SELECT AgentMachineName, DriveLetter, MAX(CollectionTimeUtc)
                        FROM CentralDiskSpaceLog
                        GROUP BY AgentMachineName, DriveLetter
                    )
                    ORDER BY AgentMachineName, DriveLetter
                ";

                using var command = new SqliteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var results = new System.Collections.Generic.List<string>();
                while (await reader.ReadAsync())
                {
                    results.Add($"{reader["AgentMachineName"]}|{reader["DriveLetter"]}|{reader["PercentFree"]}%|{reader["CollectionTimeUtc"]}");
                }

                return string.Join("\n", results);
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer ERROR] Failed to retrieve latest entries: {ex}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Counts the total number of disk space log entries in the central database.
        /// </summary>
        /// <returns>The count of entries, or -1 if an error occurred.</returns>
        public async Task<int> GetEntryCountAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                string sql = "SELECT COUNT(*) FROM CentralDiskSpaceLog";
                using var command = new SqliteCommand(sql, connection);

                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer ERROR] Failed to get entry count: {ex}");
                return -1;
            }
        }
    }
}

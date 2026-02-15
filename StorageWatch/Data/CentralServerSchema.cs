/// <summary>
/// Central Server Database Schema Initialization
/// 
/// This class handles the creation and initialization of the SQLite database schema
/// for the central server component. It creates the CentralDiskSpaceLog table and indexes
/// for efficient querying of aggregated data from multiple agents.
/// </summary>

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using StorageWatch.Services.Logging;

namespace StorageWatch.Data
{
    /// <summary>
    /// Manages SQLite database schema initialization for the central server.
    /// </summary>
    public class CentralServerSchema
    {
        private readonly string _connectionString;
        private readonly RollingFileLogger _logger;

        /// <summary>
        /// Initializes a new instance of the CentralServerSchema class.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string for the central database.</param>
        /// <param name="logger">The logger for recording database operations.</param>
        public CentralServerSchema(string connectionString, RollingFileLogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Initializes the central SQLite database, creating tables if they don't exist.
        /// This method is safe to call multiple times.
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                _logger.Log("[CentralServer] Database connection opened successfully.");

                // Create the CentralDiskSpaceLog table if it doesn't exist
                // This table stores disk space data from all agents
                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS CentralDiskSpaceLog (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        AgentMachineName TEXT NOT NULL,
                        DriveLetter TEXT NOT NULL,
                        TotalSpaceGB REAL NOT NULL,
                        UsedSpaceGB REAL NOT NULL,
                        FreeSpaceGB REAL NOT NULL,
                        PercentFree REAL NOT NULL,
                        CollectionTimeUtc DATETIME NOT NULL,
                        ReceivedAtUtc DATETIME DEFAULT CURRENT_TIMESTAMP
                    );
                ";

                using var command = new SqliteCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();
                _logger.Log("[CentralServer] CentralDiskSpaceLog table created or already exists.");

                // Create an index on (AgentMachineName, DriveLetter, CollectionTimeUtc) for efficient queries
                string createIndexSql = @"
                    CREATE INDEX IF NOT EXISTS idx_CentralDiskSpaceLog_Agent_Drive_Time
                    ON CentralDiskSpaceLog(AgentMachineName, DriveLetter, CollectionTimeUtc);
                ";

                using var indexCommand = new SqliteCommand(createIndexSql, connection);
                await indexCommand.ExecuteNonQueryAsync();
                _logger.Log("[CentralServer] Index created or already exists.");

                // Create an index on ReceivedAtUtc for cleanup operations
                string createCleanupIndexSql = @"
                    CREATE INDEX IF NOT EXISTS idx_CentralDiskSpaceLog_ReceivedAt
                    ON CentralDiskSpaceLog(ReceivedAtUtc);
                ";

                using var cleanupIndexCommand = new SqliteCommand(createCleanupIndexSql, connection);
                await cleanupIndexCommand.ExecuteNonQueryAsync();
                _logger.Log("[CentralServer] Cleanup index created or already exists.");
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer ERROR] Failed to initialize database: {ex}");
                throw;
            }
        }
    }
}

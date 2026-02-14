/// <summary>
/// SQLite Schema Initialization
/// 
/// This class handles the creation and initialization of the SQLite database schema
/// for the StorageWatch application. It creates the DiskSpaceLog table if it doesn't exist.
/// </summary>

using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using StorageWatch.Services.Logging;

namespace StorageWatch.Data
{
    /// <summary>
    /// Manages SQLite database schema initialization and setup.
    /// </summary>
    public class SqliteSchema
    {
        private readonly string _connectionString;
        private readonly RollingFileLogger _logger;

        /// <summary>
        /// Initializes a new instance of the SqliteSchema class.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string.</param>
        /// <param name="logger">The logger for recording database operations.</param>
        public SqliteSchema(string connectionString, RollingFileLogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Initializes the SQLite database, creating tables if they don't exist.
        /// This method is safe to call multiple times.
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                _logger.Log("[SQLite] Database connection opened successfully.");

                // Create the DiskSpaceLog table if it doesn't exist
                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS DiskSpaceLog (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        MachineName TEXT NOT NULL,
                        DriveLetter TEXT NOT NULL,
                        TotalSpaceGB REAL NOT NULL,
                        UsedSpaceGB REAL NOT NULL,
                        FreeSpaceGB REAL NOT NULL,
                        PercentFree REAL NOT NULL,
                        CollectionTimeUtc DATETIME NOT NULL,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );
                ";

                using var command = new SqliteCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();
                _logger.Log("[SQLite] DiskSpaceLog table created or already exists.");

                // Create an index on (MachineName, DriveLetter, CollectionTimeUtc) for efficient queries
                string createIndexSql = @"
                    CREATE INDEX IF NOT EXISTS idx_DiskSpaceLog_Machine_Drive_Time
                    ON DiskSpaceLog(MachineName, DriveLetter, CollectionTimeUtc);
                ";

                using var indexCommand = new SqliteCommand(createIndexSql, connection);
                await indexCommand.ExecuteNonQueryAsync();
                _logger.Log("[SQLite] Index created or already exists.");
            }
            catch (Exception ex)
            {
                _logger.Log($"[SQLite ERROR] Failed to initialize database: {ex}");
                throw;
            }
        }
    }
}

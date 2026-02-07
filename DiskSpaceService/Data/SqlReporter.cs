/// <summary>
/// SQL Reporter for Disk Space Data
/// 
/// This class handles the periodic insertion of disk space metrics into the SQL database.
/// It retrieves the current disk status for each monitored drive and writes the data
/// to the DiskSpaceLog table with machine name, drive letter, and space metrics.
/// </summary>

using DiskSpaceService.Config;
using DiskSpaceService.Models;
using DiskSpaceService.Services.Logging;
using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DiskSpaceService.Services.Scheduling
{
    /// <summary>
    /// Writes disk space data to a SQL database table.
    /// Records include machine name, drive letter, total/used/free space, percent free, and timestamp.
    /// </summary>
    public class SqlReporter
    {
        private readonly DiskSpaceConfig _config;
        private readonly RollingFileLogger _logger;

        /// <summary>
        /// Initializes a new instance of the SqlReporter class.
        /// </summary>
        /// <param name="config">The application configuration containing database connection string and drive list.</param>
        /// <param name="logger">The logger for recording SQL operations and errors.</param>
        public SqlReporter(DiskSpaceConfig config, RollingFileLogger logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Writes disk space data for all configured drives to the database.
        /// This method connects to SQL Server, retrieves the current status of each drive,
        /// and inserts a record into the DiskSpaceLog table. Drives that are not ready are skipped.
        /// </summary>
        public async Task WriteDailyReportAsync()
        {
            try
            {
                // Establish a connection to the SQL database
                using var connection = new SqlConnection(_config.Database.ConnectionString);
                await connection.OpenAsync();

                // Get the name of this machine for the database records
                string machineName = Environment.MachineName;

                // Process each monitored drive
                foreach (var driveLetter in _config.Drives)
                {
                    // Retrieve current disk status for this drive
                    var status = GetDiskStatus(driveLetter);

                    // Skip insertion if the drive is not ready (e.g., disconnected USB, CD-ROM without disc)
                    // A ready drive will have TotalSpaceGb > 0
                    if (status.TotalSpaceGb == 0)
                    {
                        _logger.Log($"[SQL] Skipping insert for drive {status.DriveName}: drive not ready.");
                        continue;
                    }

                    // Prepare the SQL INSERT statement with parameters to prevent SQL injection
                    string sql = @"
                        INSERT INTO DiskSpaceLog
                        (MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc)
                        VALUES (@machine, @drive, @total, @used, @free, @percent, @utc)
                    ";

                    using var command = new SqlCommand(sql, connection);
                    // Add parameters to the command with values from the disk status
                    command.Parameters.AddWithValue("@machine", machineName);
                    command.Parameters.AddWithValue("@drive", status.DriveName);
                    command.Parameters.AddWithValue("@total", status.TotalSpaceGb);
                    // Calculate used space as total minus free
                    command.Parameters.AddWithValue("@used", status.TotalSpaceGb - status.FreeSpaceGb);
                    command.Parameters.AddWithValue("@free", status.FreeSpaceGb);
                    command.Parameters.AddWithValue("@percent", status.PercentFree);
                    // Use UTC time for all database timestamps for consistency across time zones
                    command.Parameters.AddWithValue("@utc", DateTime.UtcNow);

                    try
                    {
                        // Execute the INSERT command
                        await command.ExecuteNonQueryAsync();
                        _logger.Log(
                            $"[SQL] Inserted row for drive {status.DriveName}: Free {status.FreeSpaceGb:F2} GB ({status.PercentFree:F2}%)."
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(
                            $"[SQL ERROR] Failed to insert row for drive {status.DriveName}: {ex}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[SQL ERROR] Connection or execution failure: {ex}");
            }
        }

        /// <summary>
        /// Retrieves the current disk status for a specified drive letter.
        /// Converts raw byte counts to gigabytes and handles the case where a drive is not ready.
        /// </summary>
        /// <param name="driveLetter">The drive letter to query (e.g., "C:", "D:").</param>
        /// <returns>A DiskStatus object with the current disk metrics. If the drive is not ready or an error occurs, returns a status with zero values.</returns>
        private DiskStatus GetDiskStatus(string driveLetter)
        {
            try
            {
                // Get drive information for the specified drive
                var di = new DriveInfo(driveLetter);

                // Check if the drive is ready (not all drives can be ready at all times)
                if (!di.IsReady)
                {
                    _logger.Log($"[SQL] Drive {driveLetter} is not ready.");
                    return new DiskStatus
                    {
                        DriveName = driveLetter,
                        TotalSpaceGb = 0,
                        FreeSpaceGb = 0
                    };
                }

                // Convert bytes to gigabytes: 1 GB = 1024 * 1024 * 1024 bytes
                double totalGb = di.TotalSize / 1024d / 1024d / 1024d;
                double freeGb = di.AvailableFreeSpace / 1024d / 1024d / 1024d;

                return new DiskStatus
                {
                    DriveName = driveLetter,
                    TotalSpaceGb = totalGb,
                    FreeSpaceGb = freeGb
                };
            }
            catch (Exception ex)
            {
                _logger.Log($"[SQL] Error reading drive {driveLetter}: {ex}");

                // Return a zero-valued status to indicate the drive could not be read
                return new DiskStatus
                {
                    DriveName = driveLetter,
                    TotalSpaceGb = 0,
                    FreeSpaceGb = 0
                };
            }
        }
    }
}
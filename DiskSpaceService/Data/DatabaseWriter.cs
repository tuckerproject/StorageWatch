using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using DiskSpaceService.Models;

namespace DiskSpaceService.Data
{
    public class DatabaseWriter
    {
        private readonly string _connectionString;

        public DatabaseWriter(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InsertAsync(IEnumerable<DiskMetrics> metricsList)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var metrics in metricsList)
            {
                using var command = new SqlCommand(@"
                    INSERT INTO dbo.DiskSpaceLog
                    (
                        MachineName,
                        DriveLetter,
                        TotalSpaceGB,
                        UsedSpaceGB,
                        FreeSpaceGB,
                        PercentFree,
                        CollectionTimeUtc
                    )
                    VALUES
                    (
                        @MachineName,
                        @DriveLetter,
                        @TotalSpaceGB,
                        @UsedSpaceGB,
                        @FreeSpaceGB,
                        @PercentFree,
                        SYSUTCDATETIME()
                    )", connection);

                command.Parameters.Add("@MachineName", SqlDbType.NVarChar, 128).Value = metrics.MachineName;
                command.Parameters.Add("@DriveLetter", SqlDbType.Char, 1).Value = metrics.DriveLetter;
                command.Parameters.Add("@TotalSpaceGB", SqlDbType.Decimal).Value = metrics.TotalSpaceGB;
                command.Parameters.Add("@UsedSpaceGB", SqlDbType.Decimal).Value = metrics.UsedSpaceGB;
                command.Parameters.Add("@FreeSpaceGB", SqlDbType.Decimal).Value = metrics.FreeSpaceGB;
                command.Parameters.Add("@PercentFree", SqlDbType.Decimal).Value = metrics.PercentFree;

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
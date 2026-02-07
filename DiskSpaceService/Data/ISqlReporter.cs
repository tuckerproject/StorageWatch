/// <summary>
/// SQL Reporting Interface
/// 
/// Defines the contract for SQL reporting operations including writing daily reports
/// and managing last-run timestamps. Implementations are responsible for recording
/// disk space metrics to the database.
/// </summary>

using System;

namespace DiskSpaceService.Data
{
    /// <summary>
    /// Defines the contract for writing SQL daily reports and tracking last-run timestamps.
    /// </summary>
    public interface ISqlReporter
    {
        /// <summary>
        /// Writes the daily SQL report to the database.
        /// </summary>
        Task WriteDailyReportAsync();

        /// <summary>
        /// Retrieves the last time SQL reporting successfully ran.
        /// </summary>
        DateTime GetLastRun();

        /// <summary>
        /// Updates the last-run timestamp after a successful SQL write.
        /// </summary>
        void UpdateLastRun(DateTime timestamp);
    }
}
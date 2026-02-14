/// <summary>
/// SQL Reporter Scheduler
/// 
/// This class implements the scheduling logic for disk space reporting to the SQL database.
/// It supports multiple scheduling modes:
/// - Single run per day at a scheduled time
/// - Multiple runs per day at a scheduled time
/// - Missed collection recovery: if a scheduled run is missed, it can be run as soon as the condition is met
/// 
/// The scheduler tracks the last run time in a file to maintain state across service restarts.
/// </summary>

using StorageWatch.Config;
using StorageWatch.Services.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StorageWatch.Services.Scheduling
{
    /// <summary>
    /// Manages scheduling and execution of daily SQL reporting tasks.
    /// </summary>
    public class SqlReporterScheduler
    {
        private readonly StorageWatchConfig _config;
        private readonly SqlReporter _reporter;
        private readonly RollingFileLogger _logger;

        // Path to file that stores the last time SQL reporting successfully ran
        // This allows recovery of scheduling state across service restarts
        private readonly string _lastRunPath =
            Path.Combine(AppContext.BaseDirectory, "last_sql_run.txt");

        /// <summary>
        /// Initializes a new instance of the SqlReporterScheduler class.
        /// </summary>
        /// <param name="config">Application configuration containing SQL reporting settings and collection time.</param>
        /// <param name="reporter">The SqlReporter instance that will execute the actual reporting.</param>
        /// <param name="logger">Logger for recording scheduling decisions and execution.</param>
        public SqlReporterScheduler(
            StorageWatchConfig config,
            SqlReporter reporter,
            RollingFileLogger logger)
        {
            _config = config;
            _reporter = reporter;
            _logger = logger;
        }

        /// <summary>
        /// Checks if SQL reporting should run at the given time and executes it if needed.
        /// Implements scheduling logic based on configuration (once per day, multiple times, missed collection, etc.).
        /// Updates the last-run timestamp after successful execution.
        /// </summary>
        /// <param name="now">The current datetime to evaluate against the schedule.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task CheckAndRunAsync(DateTime now)
        {
            // Early exit if SQL reporting is disabled in configuration
            if (!_config.EnableSqlReporting)
                return;

            // Calculate the scheduled time for today based on the configured CollectionTime
            // For example, if CollectionTime is "02:00", scheduled would be today at 02:00
            DateTime scheduled = now.Date + _config.CollectionTime;
            
            // Load the last time SQL reporting was executed
            DateTime? lastRun = LoadLastRun();

            bool shouldRun = false;

            if (_config.RunOnlyOncePerDay)
            {
                // ================================================================
                // Mode: Run at most once per day
                // ================================================================
                
                if (lastRun == null || lastRun.Value.Date < now.Date)
                {
                    // We have never run before or haven't run today yet
                    // Check if we've reached the scheduled time
                    if (now >= scheduled)
                        shouldRun = true;
                }
                else if (_config.RunMissedCollection && now > scheduled && lastRun.Value < scheduled)
                {
                    // We've already run today, but missed collection is enabled
                    // This handles a specific edge case: if a missed run check is made after the scheduled time
                    // but before the last run was recorded, execute it
                    shouldRun = true;
                }
            }
            else
            {
                // ================================================================
                // Mode: Can run multiple times per day at the scheduled time
                // ================================================================
                
                // Run if we've reached or passed the scheduled time today
                if (now >= scheduled)
                    shouldRun = true;
            }

            if (shouldRun)
            {
                _logger.Log("[SCHEDULER] Run: scheduled or missed run");
                // Execute the SQL reporting task
                await _reporter.WriteDailyReportAsync();
                // Record the successful run time
                SaveLastRun(now);
            }
        }

        /// <summary>
        /// Loads the timestamp of the last successful SQL reporting run from disk.
        /// Returns null if the file doesn't exist or cannot be parsed.
        /// </summary>
        /// <returns>DateTime of the last run, or null if no previous run is recorded.</returns>
        private DateTime? LoadLastRun()
        {
            try
            {
                // Check if the last-run file exists
                if (!File.Exists(_lastRunPath))
                    return null;

                // Read the file content and trim whitespace
                string text = File.ReadAllText(_lastRunPath).Trim();
                
                // Attempt to parse the content as a DateTime
                // Using "o" format (ISO 8601 round-trip format) for consistency
                if (DateTime.TryParse(text, out var dt))
                    return dt;
            }
            catch { }

            // Return null if the file doesn't exist, is empty, or contains invalid data
            return null;
        }

        /// <summary>
        /// Saves the current datetime as the last successful SQL reporting run.
        /// This timestamp is used to determine if the next run should occur.
        /// </summary>
        /// <param name="dt">The datetime to save as the last run time.</param>
        private void SaveLastRun(DateTime dt)
        {
            try
            {
                // Write the datetime in ISO 8601 format ("o" format)
                // This ensures consistent parsing across different locales and times
                File.WriteAllText(_lastRunPath, dt.ToString("o"));
            }
            catch { }
        }
    }
}
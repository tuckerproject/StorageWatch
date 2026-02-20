/// <summary>
/// Rolling File Logger
/// 
/// A thread-safe logger that writes messages to a file with automatic rotation.
/// When the log file exceeds the maximum size, older logs are archived with numbered suffixes
/// (.1, .2, .3) and a new log file is created. Only a fixed number of archived files are retained;
/// older files are automatically deleted.
/// 
/// Log rotation mechanism:
/// - service.log (current, max 1 MB)
/// - service.log.1 (archive 1, most recent)
/// - service.log.2 (archive 2)
/// - service.log.3 (archive 3, oldest before deletion)
/// </summary>

using System;
using System.IO;

namespace StorageWatch.Services.Logging
{
    /// <summary>
    /// A thread-safe file logger with automatic log rotation based on file size.
    /// </summary>
    public class RollingFileLogger
    {
        private readonly string _logFilePath;
        // Lock object to ensure thread-safe write operations when multiple threads log simultaneously
        private readonly object _lock = new();

        // Maximum size for a single log file before rotation (1 MB = 1,000,000 bytes)
        private const long MaxSizeBytes = 1_000_000;

        // Maximum number of archived log files to keep (older files are deleted)
        private const int MaxFiles = 3;

        /// <summary>
        /// Initializes a new instance of the RollingFileLogger class.
        /// Creates the log directory if it does not exist.
        /// </summary>
        /// <param name="logFilePath">The full path to the log file (e.g., "C:\ProgramData\StorageWatch\Logs\service.log").</param>
        public RollingFileLogger(string logFilePath)
        {
            _logFilePath = logFilePath;

            // Create the directory structure if it doesn't exist
            string? dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// Writes a timestamped message to the log file in a thread-safe manner.
        /// Automatically rotates logs when the file size exceeds the maximum.
        /// </summary>
        /// <param name="message">The message to write to the log file.</param>
        public void Log(string message)
        {
            lock (_lock)
            {
                // Format the message with a timestamp
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
                // Append the line to the log file
                File.AppendAllText(_logFilePath, line + Environment.NewLine);

                // Check if rotation is needed (if the file exceeds max size)
                RollIfNeeded();
            }
        }

        /// <summary>
        /// Checks if log rotation is needed and performs the rotation if the current log file
        /// has exceeded the maximum size limit.
        /// 
        /// The rotation process:
        /// 1. Deletes the oldest archived file (service.log.3)
        /// 2. Renames service.log.2 to service.log.3
        /// 3. Renames service.log.1 to service.log.2
        /// 4. Renames service.log (current) to service.log.1
        /// 5. Creates a new empty service.log file
        /// </summary>
        private void RollIfNeeded()
        {
            FileInfo fi = new FileInfo(_logFilePath);

            // If file doesn't exist or hasn't reached max size, no rotation needed
            if (!fi.Exists || fi.Length < MaxSizeBytes)
                return;

            // Delete the oldest file (service.log.3)
            string oldest = _logFilePath + ".3";
            if (File.Exists(oldest))
                File.Delete(oldest);

            // Shift .2 → .3 (rename the second-newest to oldest position)
            string two = _logFilePath + ".2";
            if (File.Exists(two))
                File.Move(two, oldest);

            // Shift .1 → .2 (rename the newest archive to second-newest)
            string one = _logFilePath + ".1";
            if (File.Exists(one))
                File.Move(one, two);

            // Shift current → .1 (archive the current log as the newest archive)
            File.Move(_logFilePath, one, true);

            // Create a new empty current log file
            File.WriteAllText(_logFilePath, string.Empty);
        }
    }
}
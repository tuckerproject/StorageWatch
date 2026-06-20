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
using System.Text;
using System.Threading;

namespace StorageWatch.Services.Logging
{
    /// <summary>
    /// A thread-safe file logger with automatic log rotation based on file size.
    /// </summary>
    public class RollingFileLogger
    {
        private readonly string _logFilePath;
        // Static lock to serialize writes across logger instances targeting the same file system.
        private static readonly object _sync = new();
        private const int WriteRetryAttempts = 3;
        private const int WriteRetryDelayMs = 25;

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
            try
            {
                lock (_sync)
                {
                    // Format the message with a timestamp
                    string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
                    var writeSucceeded = false;
                    for (int attempt = 1; attempt <= WriteRetryAttempts; attempt++)
                    {
                        try
                        {
                            // Append the line to the log file
                            using var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                            using var writer = new StreamWriter(stream, Encoding.UTF8);
                            writer.WriteLine(line);
                            writer.Flush();
                            writeSucceeded = true;
                            break;
                        }
                        catch (IOException)
                        {
                            if (attempt < WriteRetryAttempts)
                            {
                                Console.Error.WriteLine($"[DIAG-LOG] Write retry due to IOException (attempt {attempt}/{WriteRetryAttempts}).");
                                Thread.Sleep(WriteRetryDelayMs);
                                continue;
                            }

                            // Swallow final exhausted IOException to keep logging non-fatal.
                            Console.Error.WriteLine("[DIAG-LOG] Write failed after retries; log entry dropped.");
                        }
                    }

                    if (!writeSucceeded)
                        return;

                    // Check if rotation is needed (if the file exceeds max size)
                    RollIfNeeded();
                }
            }
            catch (Exception)
            {
                // Logging must remain non-fatal.
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
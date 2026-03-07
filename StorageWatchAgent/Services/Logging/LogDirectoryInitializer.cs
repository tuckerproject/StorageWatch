/// <summary>
/// Initializes the global log directory for all StorageWatch projects.
/// 
/// Creates C:\ProgramData\StorageWatch\Logs if it does not exist.
/// </summary>

using System;
using System.IO;

namespace StorageWatch.Services.Logging
{
    public static class LogDirectoryInitializer
    {
        /// <summary>
        /// Ensures the global log directory exists.
        /// Creates C:\ProgramData\StorageWatch\Logs if it doesn't exist.
        /// </summary>
        /// <returns>The path to the Logs directory.</returns>
        public static string EnsureLogDirectoryExists()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var logsDirectory = Path.Combine(programData, "StorageWatch", "Logs");
            
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }
            
            return logsDirectory;
        }

        /// <summary>
        /// Gets the full path to a project log file in the global logs directory.
        /// </summary>
        /// <param name="logFileName">The log file name (e.g., "agent.log", "server.log", "ui.log").</param>
        /// <returns>The full path to the log file.</returns>
        public static string GetLogFilePath(string logFileName)
        {
            var logsDirectory = EnsureLogDirectoryExists();
            return Path.Combine(logsDirectory, logFileName);
        }
    }
}

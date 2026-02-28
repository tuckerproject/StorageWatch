using System.IO;

namespace StorageWatchUI.Services;

/// <summary>
/// Provides runtime-resolved file paths for StorageWatch Agent application data.
/// </summary>
public class PathProvider : IPathProvider
{
    private readonly string _databasePath;
    private readonly string _logDirectory;
    private readonly string _logFilePath;

    public PathProvider()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var agentDir = Path.Combine(programData, "StorageWatch", "Agent");

        _databasePath = Path.Combine(agentDir, "StorageWatch.db");
        _logDirectory = Path.Combine(agentDir, "Logs");
        _logFilePath = Path.Combine(_logDirectory, "service.log");

        // Ensure directories exist
        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Gets the path to the StorageWatch Agent SQLite database.
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// Gets the path to the agent service log file.
    /// </summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// Gets the directory containing agent service logs.
    /// </summary>
    public string LogDirectory => _logDirectory;

    private void EnsureDirectoriesExist()
    {
        try
        {
            var dbDir = Path.GetDirectoryName(_databasePath);
            if (dbDir != null && !Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
        catch
        {
            // Ignore errors during directory creation; the application will handle missing directories gracefully
        }
    }
}

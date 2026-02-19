namespace StorageWatchUI.Services;

/// <summary>
/// Provides file paths for StorageWatch application data.
/// </summary>
public interface IPathProvider
{
    /// <summary>
    /// Gets the path to the StorageWatch SQLite database.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Gets the path to the service log file.
    /// </summary>
    string LogFilePath { get; }

    /// <summary>
    /// Gets the directory containing service logs.
    /// </summary>
    string LogDirectory { get; }
}

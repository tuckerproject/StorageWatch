using StorageWatchUI.Services;

namespace StorageWatchUI.Tests.Services;

/// <summary>
/// Mock PathProvider for testing that uses isolated test database paths.
/// </summary>
public class MockPathProvider : IPathProvider
{
    private readonly string _databasePath;
    private readonly string _logDirectory;
    private readonly string _logFilePath;

    public MockPathProvider(string testDatabasePath)
    {
        _databasePath = testDatabasePath;
        _logDirectory = Path.Combine(Path.GetDirectoryName(testDatabasePath) ?? "", "Logs");
        _logFilePath = Path.Combine(_logDirectory, "test.log");

        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Gets the path to the test SQLite database.
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// Gets the path to the test service log file.
    /// </summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// Gets the directory containing test service logs.
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
            // Ignore errors during directory creation
        }
    }
}

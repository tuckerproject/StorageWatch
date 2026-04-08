namespace StorageWatch.Updater;

/// <summary>
/// Manages backup creation and storage for update operations.
/// Provides consistent backup naming and organization without logging dependencies.
/// Designed for testability with instance methods and try-catch patterns.
/// </summary>
internal class BackupManager
{
    private readonly string _backupBaseDirectory;
    private readonly FileReplacementEngine _fileEngine;

    /// <summary>
    /// Initializes a new BackupManager instance.
    /// </summary>
    /// <param name="backupBaseDirectory">Base directory where backups will be stored. Defaults to system temp directory.</param>
    /// <param name="fileEngine">Optional FileReplacementEngine instance. If null, a new instance is created.</param>
    public BackupManager(string? backupBaseDirectory = null, FileReplacementEngine? fileEngine = null)
    {
        _backupBaseDirectory = backupBaseDirectory ?? Path.Combine(Path.GetTempPath(), "StorageWatchBackup");
        _fileEngine = fileEngine ?? new FileReplacementEngine();
    }

    /// <summary>
    /// Creates a unique backup directory for a target directory.
    /// </summary>
    /// <param name="targetDirectory">The directory to be backed up.</param>
    /// <returns>Path to the newly created backup directory, or empty string on failure.</returns>
    public string TryCreateBackupDirectory(string targetDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
                return string.Empty;

            var backupId = Guid.NewGuid().ToString("N");
            var backupDirectory = Path.Combine(_backupBaseDirectory, backupId);

            if (!_fileEngine.TryEnsureDirectoryExists(backupDirectory))
                return string.Empty;

            return backupDirectory;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates a unique backup directory for a target directory, throwing on error.
    /// </summary>
    /// <param name="targetDirectory">The directory to be backed up.</param>
    /// <returns>Path to the newly created backup directory.</returns>
    /// <exception cref="ArgumentException">Thrown when targetDirectory is null or empty.</exception>
    public string CreateBackupDirectory(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory cannot be null or empty.", nameof(targetDirectory));

        var backupDirectory = TryCreateBackupDirectory(targetDirectory);
        if (string.IsNullOrEmpty(backupDirectory))
            throw new InvalidOperationException("Failed to create backup directory.");

        return backupDirectory;
    }

    /// <summary>
    /// Creates a backup of a target directory to a backup location.
    /// </summary>
    /// <param name="targetDirectory">Directory to backup.</param>
    /// <param name="backupDirectory">Directory where backup will be stored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if backup succeeded, false otherwise.</returns>
    public bool TryBackupDirectory(string targetDirectory, string backupDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
                return false;

            if (string.IsNullOrWhiteSpace(backupDirectory))
                return false;

            if (!Directory.Exists(targetDirectory))
                return false;

            if (!_fileEngine.TryEnsureDirectoryExists(backupDirectory))
                return false;

            return _fileEngine.TryCopyDirectory(targetDirectory, backupDirectory, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a backup of a target directory to a backup location, throwing on error.
    /// </summary>
    /// <param name="targetDirectory">Directory to backup.</param>
    /// <param name="backupDirectory">Directory where backup will be stored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when paths are null or empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when target directory does not exist.</exception>
    public void BackupDirectory(string targetDirectory, string backupDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory cannot be null or empty.", nameof(targetDirectory));

        if (string.IsNullOrWhiteSpace(backupDirectory))
            throw new ArgumentException("Backup directory cannot be null or empty.", nameof(backupDirectory));

        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");

        if (!TryBackupDirectory(targetDirectory, backupDirectory, cancellationToken))
            throw new InvalidOperationException("Failed to backup directory.");
    }

    /// <summary>
    /// Validates that a backup directory exists and is accessible.
    /// </summary>
    /// <param name="backupDirectory">Path to the backup directory.</param>
    /// <returns>True if backup exists and is valid, false otherwise.</returns>
    public bool IsValidBackup(string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            return false;

        if (!Directory.Exists(backupDirectory))
            return false;

        try
        {
            // Try to enumerate directory to verify it's accessible
            _ = Directory.GetFileSystemEntries(backupDirectory);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cleans up a backup directory, deleting all its contents.
    /// </summary>
    /// <param name="backupDirectory">Path to the backup directory to clean up.</param>
    /// <returns>True if cleanup succeeded, false otherwise.</returns>
    public bool TryCleanupBackup(string backupDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
                return true;

            return _fileEngine.TryDeleteDirectory(backupDirectory);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cleans up a backup directory, deleting all its contents. Throws on error.
    /// </summary>
    /// <param name="backupDirectory">Path to the backup directory to clean up.</param>
    public void CleanupBackup(string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            return;

        _fileEngine.DeleteDirectory(backupDirectory);
    }
}

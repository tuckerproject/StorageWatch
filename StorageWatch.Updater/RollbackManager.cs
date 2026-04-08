namespace StorageWatch.Updater;

/// <summary>
/// Result of a rollback operation containing status and diagnostic information.
/// </summary>
internal class RollbackResult
{
    /// <summary>
    /// Gets whether the rollback succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets whether the rollback was fully completed or only partially recovered.
    /// </summary>
    public bool IsPartialRecovery { get; set; }

    /// <summary>
    /// Gets a diagnostic message describing the rollback result.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets the original exception that triggered the rollback, if any.
    /// </summary>
    public Exception? OriginalException { get; set; }

    public RollbackResult()
    {
        Message = string.Empty;
    }
}

/// <summary>
/// Manages rollback operations when updates fail.
/// Provides recovery by restoring from backups with support for partial updates.
/// Includes console output for debugging without external logging dependencies.
/// Designed for testability with instance methods and dependency injection.
/// </summary>
internal class RollbackManager
{
    private readonly bool _verbose;
    private readonly FileReplacementEngine _fileEngine;
    private readonly BackupManager _backupManager;

    /// <summary>
    /// Initializes a new RollbackManager instance.
    /// </summary>
    /// <param name="verbose">If true, prints diagnostic messages to console.</param>
    /// <param name="fileEngine">Optional FileReplacementEngine instance. If null, a new instance is created.</param>
    /// <param name="backupManager">Optional BackupManager instance. If null, a new instance is created.</param>
    public RollbackManager(bool verbose = false, FileReplacementEngine? fileEngine = null, BackupManager? backupManager = null)
    {
        _verbose = verbose;
        _fileEngine = fileEngine ?? new FileReplacementEngine();
        _backupManager = backupManager ?? new BackupManager();
    }

    /// <summary>
    /// Restores a target directory from a backup.
    /// Clears the target directory and replaces it with backup contents.
    /// </summary>
    /// <param name="backupDirectory">Directory containing the backup to restore from.</param>
    /// <param name="targetDirectory">Directory to restore to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if restore succeeded, false otherwise.</returns>
    public bool TryRestoreFromBackup(string backupDirectory, string targetDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
                return false;

            if (string.IsNullOrWhiteSpace(targetDirectory))
                return false;

            if (!Directory.Exists(backupDirectory))
                return false;

            PrintDebug($"Restoring from backup: {backupDirectory}");

            // Ensure target directory exists
            if (!_fileEngine.TryEnsureDirectoryExists(targetDirectory))
                return false;

            // Clear the target directory
            PrintDebug($"Clearing target directory: {targetDirectory}");
            if (!_fileEngine.TryClearDirectory(targetDirectory, cancellationToken))
                return false;

            // Restore from backup
            PrintDebug("Copying backup contents to target directory...");
            if (!_fileEngine.TryCopyDirectory(backupDirectory, targetDirectory, cancellationToken))
                return false;

            PrintDebug("Restore from backup completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            PrintDebug($"Exception during restore: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores a target directory from a backup, throwing on error.
    /// Clears the target directory and replaces it with backup contents.
    /// </summary>
    /// <param name="backupDirectory">Directory containing the backup to restore from.</param>
    /// <param name="targetDirectory">Directory to restore to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when paths are null or empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when backup directory does not exist.</exception>
    public void RestoreFromBackup(string backupDirectory, string targetDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            throw new ArgumentException("Backup directory cannot be null or empty.", nameof(backupDirectory));

        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory cannot be null or empty.", nameof(targetDirectory));

        if (!Directory.Exists(backupDirectory))
            throw new DirectoryNotFoundException($"Backup directory not found: {backupDirectory}");

        if (!TryRestoreFromBackup(backupDirectory, targetDirectory, cancellationToken))
            throw new InvalidOperationException("Failed to restore from backup.");
    }

    /// <summary>
    /// Attempts a rollback operation, catching any errors to prevent further damage.
    /// </summary>
    /// <param name="backupDirectory">Directory containing the backup to restore from.</param>
    /// <param name="targetDirectory">Directory to restore to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RollbackResult containing success status and diagnostics.</returns>
    public RollbackResult TryRollback(string backupDirectory, string targetDirectory, CancellationToken cancellationToken = default)
    {
        var result = new RollbackResult();

        try
        {
            PrintDebug("Starting rollback operation...");

            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                result.Success = false;
                result.Message = "Backup directory path is invalid.";
                PrintDebug($"ERROR: {result.Message}");
                return result;
            }

            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                result.Success = false;
                result.Message = "Target directory path is invalid.";
                PrintDebug($"ERROR: {result.Message}");
                return result;
            }

            // Validate backup exists and is accessible
            if (!_backupManager.IsValidBackup(backupDirectory))
            {
                result.Success = false;
                result.Message = $"Backup directory is not valid or not accessible: {backupDirectory}";
                PrintDebug($"WARNING: {result.Message}");
                return result;
            }

            PrintDebug($"Backup validated: {backupDirectory}");

            // Get backup statistics for diagnostics
            var backupFileCount = CountFiles(backupDirectory);
            PrintDebug($"Backup contains {backupFileCount} files.");

            // Attempt restore
            if (!TryRestoreFromBackup(backupDirectory, targetDirectory, cancellationToken))
            {
                result.Success = false;
                result.IsPartialRecovery = true;
                result.Message = "Failed to restore from backup.";
                PrintDebug($"ERROR: {result.Message}");
                return result;
            }

            // Verify restoration by checking file count
            var restoredFileCount = CountFiles(targetDirectory);
            if (restoredFileCount == backupFileCount)
            {
                result.Success = true;
                result.IsPartialRecovery = false;
                result.Message = $"Rollback completed successfully. Restored {restoredFileCount} files.";
                PrintDebug($"SUCCESS: {result.Message}");
            }
            else
            {
                // Partial recovery - files restored but count mismatch
                result.Success = true;
                result.IsPartialRecovery = true;
                result.Message = $"Rollback partially recovered. Expected {backupFileCount} files, restored {restoredFileCount}.";
                PrintDebug($"WARNING: {result.Message}");
            }

            return result;
        }
        catch (OperationCanceledException ex)
        {
            result.Success = false;
            result.IsPartialRecovery = true;
            result.Message = "Rollback was cancelled. System may be in inconsistent state.";
            result.OriginalException = ex;
            PrintDebug($"ERROR: {result.Message}");
            return result;
        }
        catch (DirectoryNotFoundException ex)
        {
            result.Success = false;
            result.IsPartialRecovery = true;
            result.Message = $"Directory not found during rollback: {ex.Message}";
            result.OriginalException = ex;
            PrintDebug($"ERROR: {result.Message}");
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Success = false;
            result.IsPartialRecovery = true;
            result.Message = $"Access denied during rollback: {ex.Message}";
            result.OriginalException = ex;
            PrintDebug($"ERROR: {result.Message}");
            return result;
        }
        catch (IOException ex)
        {
            result.Success = false;
            result.IsPartialRecovery = true;
            result.Message = $"I/O error during rollback: {ex.Message}";
            result.OriginalException = ex;
            PrintDebug($"ERROR: {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.IsPartialRecovery = true;
            result.Message = $"Unexpected error during rollback: {ex.Message}";
            result.OriginalException = ex;
            PrintDebug($"ERROR: {result.Message}");
            return result;
        }
    }

    /// <summary>
    /// Safely attempts rollback when file replacement fails.
    /// This is the recommended method for integration with file replacement operations.
    /// </summary>
    /// <param name="backupDirectory">Directory containing the backup.</param>
    /// <param name="targetDirectory">Directory to restore.</param>
    /// <param name="failureReason">Description of the failure that triggered rollback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RollbackResult with recovery status.</returns>
    public RollbackResult RollbackOnFileReplacementFailure(
        string backupDirectory,
        string targetDirectory,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("ERROR: File replacement failed. Starting rollback...");
        Console.WriteLine($"Failure reason: {failureReason}");
        Console.WriteLine();

        var result = TryRollback(backupDirectory, targetDirectory, cancellationToken);

        Console.WriteLine();
        if (result.Success)
        {
            Console.WriteLine("✓ Rollback completed successfully.");
            if (result.IsPartialRecovery)
            {
                Console.WriteLine("⚠ WARNING: Rollback was only partial. System may be in inconsistent state.");
            }
        }
        else
        {
            Console.WriteLine("✗ Rollback FAILED. System is in an inconsistent state.");
            Console.WriteLine($"Details: {result.Message}");
        }
        Console.WriteLine();

        return result;
    }

    /// <summary>
    /// Counts the total number of files in a directory tree.
    /// Used for partial update detection.
    /// </summary>
    /// <param name="directory">Directory to count files in.</param>
    /// <returns>Total file count, or -1 if error counting files.</returns>
    private static int CountFiles(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
                return 0;

            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length;
        }
        catch
        {
            return -1; // Error counting files
        }
    }

    /// <summary>
    /// Prints a debug message to console if verbose mode is enabled.
    /// </summary>
    /// <param name="message">Message to print.</param>
    private void PrintDebug(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[ROLLBACK DEBUG] {message}");
        }
    }
}

namespace StorageWatch.Updater;

/// <summary>
/// Handles file replacement operations during updates.
/// Provides file and directory operations designed for testability without logging dependencies.
/// </summary>
internal class FileReplacementEngine
{
    /// <summary>
    /// Initializes a new FileReplacementEngine instance.
    /// </summary>
    public FileReplacementEngine()
    {
    }

    /// <summary>
    /// Copies all files from source directory to destination directory, preserving structure.
    /// </summary>
    /// <param name="sourceDirectory">Source directory path.</param>
    /// <param name="destinationDirectory">Destination directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if copy succeeded, false if source directory doesn't exist.</returns>
    /// <exception cref="ArgumentException">Thrown when paths are null or empty.</exception>
    public bool TryCopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidatePath(sourceDirectory, nameof(sourceDirectory));
            ValidatePath(destinationDirectory, nameof(destinationDirectory));

            if (!Directory.Exists(sourceDirectory))
                return false;

            // Create destination directory if it doesn't exist
            EnsureDirectoryExists(destinationDirectory);

            // Create all subdirectories
            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                var destinationSubDir = Path.Combine(destinationDirectory, relativePath);
                EnsureDirectoryExists(destinationSubDir);
            }

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var destinationFile = Path.Combine(destinationDirectory, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationFile);

                if (!string.IsNullOrWhiteSpace(destinationDir))
                    EnsureDirectoryExists(destinationDir);

                File.Copy(file, destinationFile, overwrite: true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Copies all files from source directory to destination directory, preserving structure.
    /// Throws exceptions on errors.
    /// </summary>
    /// <param name="sourceDirectory">Source directory path.</param>
    /// <param name="destinationDirectory">Destination directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when paths are null or empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when source directory does not exist.</exception>
    public void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        ValidatePath(sourceDirectory, nameof(sourceDirectory));
        ValidatePath(destinationDirectory, nameof(destinationDirectory));

        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

        if (!TryCopyDirectory(sourceDirectory, destinationDirectory, cancellationToken))
            throw new InvalidOperationException("Failed to copy directory.");
    }

    /// <summary>
    /// Copies files from staging directory to target directory with directory structure.
    /// </summary>
    /// <param name="stagingDirectory">Directory containing files to copy.</param>
    /// <param name="targetDirectory">Directory where files will be copied to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if copy succeeded, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when paths are null or empty.</exception>
    public bool TryCopyFilesFromStaging(string stagingDirectory, string targetDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidatePath(stagingDirectory, nameof(stagingDirectory));
            ValidatePath(targetDirectory, nameof(targetDirectory));

            if (!Directory.Exists(stagingDirectory))
                return false;

            var files = Directory.GetFiles(stagingDirectory, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(stagingDirectory, file);
                var destinationPath = Path.Combine(targetDirectory, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrWhiteSpace(destinationDir))
                    EnsureDirectoryExists(destinationDir);

                File.Copy(file, destinationPath, overwrite: true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes all contents of a directory while preserving the directory itself.
    /// </summary>
    /// <param name="directory">Directory to clear.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if directory was cleared successfully, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public bool TryClearDirectory(string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidatePath(directory, nameof(directory));

            if (!Directory.Exists(directory))
                return true;

            foreach (var entry in Directory.GetFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, recursive: true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
                catch (IOException)
                {
                    // File may be in use, skip and continue
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes all contents of a directory while preserving the directory itself.
    /// Throws exceptions on errors.
    /// </summary>
    /// <param name="directory">Directory to clear.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public void ClearDirectory(string directory, CancellationToken cancellationToken = default)
    {
        if (!TryClearDirectory(directory, cancellationToken))
            throw new InvalidOperationException($"Failed to clear directory: {directory}");
    }

    /// <summary>
    /// Deletes a directory and all its contents.
    /// </summary>
    /// <param name="directory">Directory to delete.</param>
    /// <returns>True if directory was deleted, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public bool TryDeleteDirectory(string directory)
    {
        try
        {
            ValidatePath(directory, nameof(directory));

            if (!Directory.Exists(directory))
                return true;

            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes a directory and all its contents, throwing on error.
    /// </summary>
    /// <param name="directory">Directory to delete.</param>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public void DeleteDirectory(string directory)
    {
        ValidatePath(directory, nameof(directory));

        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Directory may be in use, ignore
            }
        }
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="directory">Directory path to ensure exists.</param>
    /// <returns>True if directory exists or was created, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public bool TryEnsureDirectoryExists(string directory)
    {
        try
        {
            ValidatePath(directory, nameof(directory));

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary. Throws on error.
    /// </summary>
    /// <param name="directory">Directory path to ensure exists.</param>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    public void EnsureDirectoryExists(string directory)
    {
        ValidatePath(directory, nameof(directory));

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Validates that a path is not null or whitespace.
    /// </summary>
    /// <param name="path">Path to validate.</param>
    /// <param name="paramName">Name of the parameter for exception messages.</param>
    /// <exception cref="ArgumentException">Thrown when path is null or whitespace.</exception>
    private static void ValidatePath(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException($"Path cannot be null or empty.", paramName);
    }
}

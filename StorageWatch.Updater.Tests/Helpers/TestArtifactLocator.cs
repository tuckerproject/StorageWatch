namespace StorageWatch.Updater.Tests.Helpers;

public static class TestArtifactLocator
{
    public static string FindRepositoryRoot()
    {
        var path = AppContext.BaseDirectory;
        while (true)
        {
            var gitPath = Path.Combine(path, ".git");
            if (Directory.Exists(gitPath))
                return path;

            if (File.Exists(gitPath))
            {
                ValidateWorktreeGitDirectory(path, gitPath);
                return path;
            }

            var parent = Directory.GetParent(path);
            if (parent is null)
            {
                throw new InvalidOperationException(
                    $"Could not find repository root while ascending from '{AppContext.BaseDirectory}'.");
            }

            path = parent.FullName;
        }
    }

    private static void ValidateWorktreeGitDirectory(string repositoryRoot, string gitFilePath)
    {
        var gitFileContents = File.ReadAllText(gitFilePath).Trim();
        var separatorIndex = gitFileContents.IndexOf(':');
        if (separatorIndex < 0
            || !string.Equals(gitFileContents[..separatorIndex].Trim(), "gitdir", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Malformed worktree Git metadata in '{gitFilePath}'. Expected a 'gitdir:' pointer.");
        }

        var gitDirectoryPointer = gitFileContents[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(gitDirectoryPointer)
            || gitDirectoryPointer.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new InvalidOperationException(
                $"Malformed worktree Git metadata in '{gitFilePath}'. The 'gitdir:' pointer is missing or invalid.");
        }

        string gitDirectory;
        try
        {
            gitDirectory = Path.GetFullPath(
                Path.IsPathRooted(gitDirectoryPointer)
                    ? gitDirectoryPointer
                    : Path.Combine(repositoryRoot, gitDirectoryPointer));
        }
        catch (Exception exception) when (exception is ArgumentException
            || exception is NotSupportedException
            || exception is PathTooLongException)
        {
            throw new InvalidOperationException(
                $"Malformed worktree Git metadata in '{gitFilePath}'. The 'gitdir:' pointer is not a valid path.",
                exception);
        }

        if (!Directory.Exists(gitDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Worktree Git metadata in '{gitFilePath}' points to missing Git directory '{gitDirectory}'.");
        }
    }

    public static string GetUpdaterExePath(string configuration = "Debug")
    {
        var repoRoot = FindRepositoryRoot();
        return Path.Combine(repoRoot, "StorageWatch.Updater", "bin", configuration, "net10.0", "StorageWatch.Updater.exe");
    }

    public static string GetUpdaterPackagePath(string configuration = "Debug")
    {
        var repoRoot = FindRepositoryRoot();
        return Path.Combine(repoRoot, "StorageWatch.Updater", "bin", configuration, "net10.0", "StorageWatch.Updater.zip");
    }
}

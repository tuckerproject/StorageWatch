namespace StorageWatch.Updater.Tests.Helpers;

public static class TestArtifactLocator
{
    public static string FindRepositoryRoot()
    {
        var path = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(path, ".git")) && path != Path.GetPathRoot(path))
        {
            path = Directory.GetParent(path)!.FullName;
        }

        if (!Directory.Exists(Path.Combine(path, ".git")))
            throw new InvalidOperationException("Could not find repository root.");

        return path;
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

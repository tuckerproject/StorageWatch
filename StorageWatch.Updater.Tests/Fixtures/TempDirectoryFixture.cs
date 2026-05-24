namespace StorageWatch.Updater.Tests.Fixtures;

public sealed class TempDirectoryFixture : IDisposable
{
    private readonly List<string> _paths = new();

    public string RootPath { get; }

    public TempDirectoryFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "StorageWatchUpdaterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        _paths.Add(RootPath);
    }

    public string CreateDirectory(string relativePath)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(path);
        _paths.Add(path);
        return path;
    }

    public string CreateFile(string relativePath, string content = "")
    {
        var path = GetPath(relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
        return path;
    }

    public string GetPath(string relativePath)
    {
        return Path.Combine(RootPath, relativePath);
    }

    public void Dispose()
    {
        foreach (var path in _paths.OrderByDescending(p => p.Length).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch
            {
                Thread.Sleep(50);
            }
        }
    }
}

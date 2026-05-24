using System.IO.Compression;

namespace StorageWatch.Updater.Tests.Helpers;

public static class ZipTestUtilities
{
    public static string ExtractToTemp(string zipPath)
    {
        var extractionPath = Path.Combine(Path.GetTempPath(), "StorageWatchUpdaterTests", "extract_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionPath);
        ZipFile.ExtractToDirectory(zipPath, extractionPath, overwriteFiles: true);
        return extractionPath;
    }

    public static IReadOnlyCollection<string> ListEntries(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return archive.Entries.Select(e => e.FullName).ToArray();
    }
}

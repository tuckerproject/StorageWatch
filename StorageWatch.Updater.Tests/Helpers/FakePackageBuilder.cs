using System.IO.Compression;

namespace StorageWatch.Updater.Tests.Helpers;

public static class FakePackageBuilder
{
    public static string CreateZipFromFiles(string zipPath, params (string RelativePath, string Content)[] files)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), "StorageWatchUpdaterTests", "pkg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);

        foreach (var file in files)
        {
            var fullPath = Path.Combine(stagingDir, file.RelativePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, file.Content);
        }

        var destinationDir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrWhiteSpace(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(stagingDir, zipPath);
        Directory.Delete(stagingDir, recursive: true);
        return zipPath;
    }

    public static string CreateCorruptZip(string zipPath)
    {
        var destinationDir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrWhiteSpace(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        File.WriteAllText(zipPath, "not-a-valid-zip");
        return zipPath;
    }
}

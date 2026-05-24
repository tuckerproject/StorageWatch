using StorageWatch.Updater.Tests.Helpers;

namespace StorageWatch.Updater.Tests.Unit;

public class StagingDirectoryTests : IDisposable
{
    private readonly string _root;

    public StagingDirectoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "StorageWatchUpdaterTests", "staging_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateZipAndExtract_WhenPackageValid_ShouldCreateExpectedFiles()
    {
        var zipPath = Path.Combine(_root, "update.zip");
        FakePackageBuilder.CreateZipFromFiles(zipPath,
            ("StorageWatch.Updater.exe", "fake-binary"),
            ("config/settings.json", "{}"));

        var extractPath = ZipTestUtilities.ExtractToTemp(zipPath);

        File.Exists(Path.Combine(extractPath, "StorageWatch.Updater.exe")).Should().BeTrue();
        File.Exists(Path.Combine(extractPath, "config", "settings.json")).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ListEntries_WhenZipCreated_ShouldReturnAllEntries()
    {
        var zipPath = Path.Combine(_root, "contents.zip");
        FakePackageBuilder.CreateZipFromFiles(zipPath,
            ("a.txt", "A"),
            ("nested/b.txt", "B"));

        var entries = ZipTestUtilities.ListEntries(zipPath);

        entries.Should().Contain("a.txt");
        entries.Should().Contain("nested/b.txt");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }
}

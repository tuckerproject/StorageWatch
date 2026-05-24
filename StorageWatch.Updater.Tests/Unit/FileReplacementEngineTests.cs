using StorageWatch.Updater;
using StorageWatch.Updater.Tests.Fixtures;

namespace StorageWatch.Updater.Tests.Unit;

public class FileReplacementEngineTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private readonly FileReplacementEngine _engine = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void TryCopyDirectory_WhenSourceExists_ShouldCopyRecursively()
    {
        var source = _temp.CreateDirectory("source");
        var destination = _temp.CreateDirectory("dest");
        File.WriteAllText(Path.Combine(source, "a.txt"), "A");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "nested", "b.txt"), "B");

        var result = _engine.TryCopyDirectory(source, destination);

        result.Should().BeTrue();
        File.ReadAllText(Path.Combine(destination, "a.txt")).Should().Be("A");
        File.ReadAllText(Path.Combine(destination, "nested", "b.txt")).Should().Be("B");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryCopyFilesFromStaging_WhenSourceMissing_ShouldReturnFalse()
    {
        var missing = _temp.GetPath("missing");
        var target = _temp.CreateDirectory("target");

        var result = _engine.TryCopyFilesFromStaging(missing, target);

        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryCopyFilesFromStaging_WhenCanceled_ShouldReturnFalse()
    {
        var source = _temp.CreateDirectory("staging");
        var target = _temp.CreateDirectory("target");
        File.WriteAllText(Path.Combine(source, "file.txt"), "content");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = _engine.TryCopyFilesFromStaging(source, target, cts.Token);

        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryClearDirectory_WhenDirectoryExists_ShouldRemoveChildrenOnly()
    {
        var dir = _temp.CreateDirectory("clear-me");
        File.WriteAllText(Path.Combine(dir, "file.txt"), "x");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));

        var result = _engine.TryClearDirectory(dir);

        result.Should().BeTrue();
        Directory.Exists(dir).Should().BeTrue();
        Directory.GetFileSystemEntries(dir).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryEnsureDirectoryExists_WhenMissing_ShouldCreateDirectory()
    {
        var dir = _temp.GetPath("new-dir");

        var result = _engine.TryEnsureDirectoryExists(dir);

        result.Should().BeTrue();
        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryDeleteDirectory_WhenExists_ShouldDeleteDirectory()
    {
        var dir = _temp.CreateDirectory("delete-me");
        File.WriteAllText(Path.Combine(dir, "file.txt"), "content");

        var result = _engine.TryDeleteDirectory(dir);

        result.Should().BeTrue();
        Directory.Exists(dir).Should().BeFalse();
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}

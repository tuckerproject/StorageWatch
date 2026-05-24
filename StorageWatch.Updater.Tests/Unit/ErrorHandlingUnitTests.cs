using StorageWatch.Shared.Update.Models;
using StorageWatch.Updater;
using StorageWatch.Updater.Tests.Fixtures;
using StorageWatch.Updater.Tests.Helpers;
using System.IO.Compression;
using System.Net;

namespace StorageWatch.Updater.Tests.Unit;

public class ErrorHandlingUnitTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunSelfUpdateStageAsync_WhenManifestFileMissing_ShouldThrowFileNotFoundException()
    {
        var updaterExe = _temp.CreateFile("updater/StorageWatch.Updater.exe", string.Empty);
        var updaterFolder = Path.GetDirectoryName(updaterExe)!;
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var processLauncher = new FakeProcessLauncher();
        var manager = new SelfUpdateManager(updaterExe, updaterFolder, httpClient, processLauncher, _ => { });

        var missingManifestPath = _temp.GetPath("missing-manifest.json");

        var action = async () => await manager.RunSelfUpdateStageAsync(missingManifestPath, new UpdaterArguments { UpdateUI = true });

        await action.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunLegacySelfUpdateStageAsync_WhenPackageMissing_ShouldThrowHttpRequestException()
    {
        var updaterExe = _temp.CreateFile("updater/StorageWatch.Updater.exe", string.Empty);
        var updaterFolder = Path.GetDirectoryName(updaterExe)!;

        using var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var processLauncher = new FakeProcessLauncher();
        var manager = new SelfUpdateManager(updaterExe, updaterFolder, httpClient, processLauncher, _ => { });

        var component = new ComponentUpdateInfo
        {
            Version = "99.0.0.0",
            DownloadUrl = "https://updates.test/missing-updater.zip",
            Sha256 = new string('a', 64)
        };

        var action = async () => await manager.RunLegacySelfUpdateStageAsync(component, new UpdaterArguments { UpdateUI = true });

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunLegacySelfUpdateStageAsync_WhenPackageCorruptButHashMatches_ShouldThrowInvalidDataException()
    {
        var updaterExe = _temp.CreateFile("updater/StorageWatch.Updater.exe", string.Empty);
        var updaterFolder = Path.GetDirectoryName(updaterExe)!;

        var corruptBytes = System.Text.Encoding.UTF8.GetBytes("not-a-zip");
        var corruptHash = HashTestUtilities.ComputeSha256(corruptBytes);
        using var httpClient = new HttpClient(FakeHttpMessageHandler.FromBytes("https://updates.test/updater.zip", corruptBytes));
        var processLauncher = new FakeProcessLauncher();
        var manager = new SelfUpdateManager(updaterExe, updaterFolder, httpClient, processLauncher, _ => { });

        var component = new ComponentUpdateInfo
        {
            Version = "99.0.0.0",
            DownloadUrl = "https://updates.test/updater.zip",
            Sha256 = corruptHash
        };

        var action = async () => await manager.RunLegacySelfUpdateStageAsync(component, new UpdaterArguments { UpdateUI = true });

        await action.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunSelfUpdateApplyAsync_WhenArgumentsInvalid_ShouldThrowInvalidOperationException()
    {
        var updaterExe = _temp.CreateFile("updater/StorageWatch.Updater.exe", string.Empty);
        var updaterFolder = Path.GetDirectoryName(updaterExe)!;
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var processLauncher = new FakeProcessLauncher();
        var manager = new SelfUpdateManager(updaterExe, updaterFolder, httpClient, processLauncher, _ => { });

        var args = new UpdaterArguments
        {
            SelfUpdateApply = true,
            TargetPath = _temp.GetPath("target")
        };

        var action = async () => await manager.RunSelfUpdateApplyAsync(args);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*staging path*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ArgumentParser_WhenInvalidArgsProvided_ShouldReturnErrors()
    {
        var parser = new ArgumentParser();

        var result = parser.TryParse(new[] { "--self-update-apply", "--manifest" });

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}

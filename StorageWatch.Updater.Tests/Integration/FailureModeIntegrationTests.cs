using StorageWatch.Shared.Update.Models;
using StorageWatch.Updater;
using StorageWatch.Updater.Tests.Fixtures;
using StorageWatch.Updater.Tests.Helpers;
using System.Net;

namespace StorageWatch.Updater.Tests.Integration;

[Collection("UpdaterIntegrationSequential")]
public class FailureModeIntegrationTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdaterExe_WhenSelfUpdateStageManifestMissing_ShouldExitWithInvalidArguments()
    {
        var updaterExe = TestArtifactLocator.GetUpdaterExePath();
        if (!File.Exists(updaterExe))
        {
            return;
        }

        var missingManifest = _temp.GetPath("missing-manifest.json");

        var (exitCode, _, _) = await ProcessTestUtilities.RunProcessAsync(
            updaterExe,
            $"--self-update-stage --manifest \"{missingManifest}\"");

        exitCode.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdaterExe_WhenTargetFileLockedDuringUpdate_ShouldExitWithUnexpectedError()
    {
        var updaterExe = TestArtifactLocator.GetUpdaterExePath();
        if (!File.Exists(updaterExe))
        {
            return;
        }

        var source = _temp.CreateDirectory("locked-source");
        var target = _temp.CreateDirectory("locked-target");

        var fileName = "locked.txt";
        File.WriteAllText(Path.Combine(source, fileName), "new-content");
        File.WriteAllText(Path.Combine(target, fileName), "old-content");

        using var lockStream = new FileStream(Path.Combine(target, fileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var (exitCode, _, _) = await ProcessTestUtilities.RunProcessAsync(
            updaterExe,
            $"--update-ui --source \"{source}\" --target \"{target}\"");

        exitCode.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SelfUpdateManager_WhenUpdaterPackageCorrupt_ShouldThrowInvalidDataException()
    {
        var updaterExe = _temp.CreateFile("updater/StorageWatch.Updater.exe", string.Empty);
        var updaterFolder = Path.GetDirectoryName(updaterExe)!;

        var corruptBytes = System.Text.Encoding.UTF8.GetBytes("not-a-valid-zip");
        var expectedHash = HashTestUtilities.ComputeSha256(corruptBytes);

        using var httpClient = new HttpClient(FakeHttpMessageHandler.FromBytes("https://updates.test/updater.zip", corruptBytes, HttpStatusCode.OK));
        var launcher = new FakeProcessLauncher();
        var manager = new SelfUpdateManager(updaterExe, updaterFolder, httpClient, launcher, _ => { });

        var component = new ComponentUpdateInfo
        {
            Version = "99.0.0.0",
            DownloadUrl = "https://updates.test/updater.zip",
            Sha256 = expectedHash
        };

        var action = async () => await manager.RunLegacySelfUpdateStageAsync(component, new UpdaterArguments { UpdateUI = true });

        await action.Should().ThrowAsync<InvalidDataException>();
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}

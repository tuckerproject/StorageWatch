using StorageWatch.Updater.Tests.Fixtures;
using StorageWatch.Updater.Tests.Helpers;

namespace StorageWatch.Updater.Tests.Integration;

[Collection("UpdaterIntegrationSequential")]
public class SelfUpdateFlowIntegrationTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SelfUpdateApply_WhenValidStagingProvided_ShouldReplaceTargetFolder()
    {
        var updaterExe = TestArtifactLocator.GetUpdaterExePath();
        if (!File.Exists(updaterExe))
        {
            return;
        }

        var staging = _temp.CreateDirectory("staging");
        var target = _temp.CreateDirectory("target");

        File.Copy(updaterExe, Path.Combine(staging, "StorageWatch.Updater.exe"), overwrite: true);
        File.WriteAllText(Path.Combine(staging, "new-version.txt"), "v2");
        File.WriteAllText(Path.Combine(target, "old-version.txt"), "v1");

        var (exitCode, stdOut, stdErr) = await ProcessTestUtilities.RunProcessAsync(
            updaterExe,
            $"--self-update-apply --self-update-staging \"{staging}\" --target \"{target}\"");

        exitCode.Should().Be(0, $"stdout: {stdOut}{Environment.NewLine}stderr: {stdErr}");
        File.Exists(Path.Combine(target, "new-version.txt")).Should().BeTrue();
        File.Exists(Path.Combine(target, "old-version.txt")).Should().BeFalse();
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}

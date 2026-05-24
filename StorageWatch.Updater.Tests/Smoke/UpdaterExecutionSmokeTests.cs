using StorageWatch.Updater.Tests.Fixtures;
using StorageWatch.Updater.Tests.Helpers;

namespace StorageWatch.Updater.Tests.Smoke;

public class UpdaterExecutionSmokeTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task UpdaterRunWithoutComponentContext_WhenRestartAgentFlagProvided_ShouldExitSuccess()
    {
        var updaterExe = TestArtifactLocator.GetUpdaterExePath();
        if (!File.Exists(updaterExe))
        {
            Assert.True(true, $"Skipping: updater executable not found at {updaterExe}");
            return;
        }

        var (exitCode, _, _) = await ProcessTestUtilities.RunProcessAsync(
            updaterExe,
            "--restart-agent",
            environmentVariables: new Dictionary<string, string?>
            {
                ["STORAGEWATCH_AGENT_SERVICE_NAME"] = "StorageWatchAgent_DoesNotExist_Test"
            });

        exitCode.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task UpdaterRunWithComponentContext_WhenUiUpdateArgsProvided_ShouldExitSuccess()
    {
        var updaterExe = TestArtifactLocator.GetUpdaterExePath();
        if (!File.Exists(updaterExe))
        {
            Assert.True(true, $"Skipping: updater executable not found at {updaterExe}");
            return;
        }

        var source = _temp.CreateDirectory("smoke-ui-source");
        var target = _temp.CreateDirectory("smoke-ui-target");

        File.WriteAllText(Path.Combine(source, "payload.txt"), "smoke");
        File.Copy(Path.Combine(Environment.SystemDirectory, "whoami.exe"), Path.Combine(target, "StorageWatchUI.exe"), overwrite: true);

        var (exitCode, _, _) = await ProcessTestUtilities.RunProcessAsync(
            updaterExe,
            $"--update-ui --source \"{source}\" --target \"{target}\"");

        exitCode.Should().Be(0);
        File.Exists(Path.Combine(target, "payload.txt")).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task UpdaterRunWithValidManifest_WhenSelfUpdateStageUsed_ShouldExitSuccess()
    {
        var updaterExe = TestArtifactLocator.GetUpdaterExePath();
        if (!File.Exists(updaterExe))
        {
            Assert.True(true, $"Skipping: updater executable not found at {updaterExe}");
            return;
        }

        var manifestPath = _temp.GetPath("valid-manifest.json");
        var manifestJson = "{\"version\":\"1.0.0\",\"updater\":{\"version\":\"0.0.0.0\",\"downloadUrl\":\"https://updates.test/updater.zip\",\"sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}}";
        File.WriteAllText(manifestPath, manifestJson);

        var (exitCode, _, _) = await ProcessTestUtilities.RunProcessAsync(
            updaterExe,
            $"--self-update-stage --manifest \"{manifestPath}\"");

        exitCode.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task UpdaterRunMissingManifest_WhenSelfUpdateStageUsed_ShouldExitInvalidArguments()
    {
        var updaterExe = TestArtifactLocator.GetUpdaterExePath();
        if (!File.Exists(updaterExe))
        {
            Assert.True(true, $"Skipping: updater executable not found at {updaterExe}");
            return;
        }

        var missingManifest = _temp.GetPath("missing-manifest.json");

        var (exitCode, _, _) = await ProcessTestUtilities.RunProcessAsync(
            updaterExe,
            $"--self-update-stage --manifest \"{missingManifest}\"");

        exitCode.Should().Be(1);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}

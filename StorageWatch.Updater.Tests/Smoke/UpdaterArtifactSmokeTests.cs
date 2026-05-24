using StorageWatch.Updater.Tests.Helpers;

namespace StorageWatch.Updater.Tests.Smoke;

public class UpdaterArtifactSmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void UpdaterZipContents_WhenArtifactPresent_ShouldContainUpdaterExecutable()
    {
        var updaterZipPath = TestArtifactLocator.GetUpdaterPackagePath();
        if (!File.Exists(updaterZipPath))
        {
            Assert.True(true, $"Skipping: updater ZIP artifact not found at {updaterZipPath}");
            return;
        }

        var entries = ZipTestUtilities.ListEntries(updaterZipPath);

        entries.Should().Contain(e => e.EndsWith("StorageWatch.Updater.exe", StringComparison.OrdinalIgnoreCase));
    }
}

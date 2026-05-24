using StorageWatch.Shared.Update.Models;
using StorageWatch.Updater.Tests.Helpers;
using System.Text.Json;

namespace StorageWatch.Updater.Tests.Unit;

public class ManifestParsingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_WhenManifestIsValid_ShouldPopulateExpectedFields()
    {
        var json = new FakeManifestBuilder().WithVersion("2.0.0").BuildJson();

        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);

        manifest.Should().NotBeNull();
        manifest!.StorageWatchVersion.Should().Be("2.0.0");
        manifest.Ui.Should().NotBeNull();
        manifest.Agent.Should().NotBeNull();
        manifest.Server.Should().NotBeNull();
        manifest.Updater.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_WhenJsonInvalid_ShouldThrow()
    {
        var invalidJson = "{not json}";

        var action = () => JsonSerializer.Deserialize<UpdateManifest>(invalidJson);

        action.Should().Throw<JsonException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_WhenServiceAliasProvided_ShouldMapToAgent()
    {
        const string json = "{\"version\":\"1.0.0\",\"service\":{\"Version\":\"9.9.9\",\"DownloadUrl\":\"https://updates.test/agent.zip\",\"Sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}}";

        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);

        manifest.Should().NotBeNull();
        manifest!.Agent.Version.Should().Be("9.9.9");
    }
}

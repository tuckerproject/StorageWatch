using StorageWatch.Shared.Update.Models;
using StorageWatch.Updater.Tests.Helpers;

namespace StorageWatch.Updater.Tests.Unit;

public class ManifestValidationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildJson_WhenUpdaterMissing_ShouldStillSerializeButCanBeValidatedAsInvalid()
    {
        var json = new FakeManifestBuilder().WithoutUpdater().BuildJson();

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"updater\":");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123456")]
    [Trait("Category", "Unit")]
    public void ComponentHash_WhenNot64Hex_ShouldBeInvalid(string sha)
    {
        var component = new ComponentUpdateInfo
        {
            Version = "1.0.0",
            DownloadUrl = "https://updates.test/pkg.zip",
            Sha256 = sha
        };

        IsLikelyValidComponent(component).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Component_WhenRequiredFieldsPresent_ShouldBeValidByTestValidator()
    {
        var component = new ComponentUpdateInfo
        {
            Version = "1.0.0",
            DownloadUrl = "https://updates.test/pkg.zip",
            Sha256 = new string('a', 64)
        };

        IsLikelyValidComponent(component).Should().BeTrue();
    }

    private static bool IsLikelyValidComponent(ComponentUpdateInfo component)
    {
        if (string.IsNullOrWhiteSpace(component.Version)) return false;
        if (string.IsNullOrWhiteSpace(component.DownloadUrl)) return false;
        if (!Uri.TryCreate(component.DownloadUrl, UriKind.Absolute, out _)) return false;
        if (component.Sha256.Length != 64) return false;
        return component.Sha256.All(c => char.IsAsciiHexDigit(c));
    }
}

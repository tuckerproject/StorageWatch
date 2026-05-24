using StorageWatch.Updater;

namespace StorageWatch.Updater.Tests.Unit;

public class ArgumentParserTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenAllSupportedFlagsProvided_ShouldParseSuccessfully()
    {
        var parser = new ArgumentParser();
        var args = new[]
        {
            "--self-update-stage",
            "--update-ui",
            "--update-agent",
            "--update-server",
            "--restart-ui",
            "--restart-agent",
            "--restart-server",
            "--manifest", "C:/temp/manifest.json",
            "--source", "C:/temp/source",
            "--target", "C:/temp/target",
            "--self-update-staging", "C:/temp/staging",
            "--continue-args", "ZW1wdHk="
        };

        var result = parser.TryParse(args);

        result.Success.Should().BeTrue();
        result.Arguments.SelfUpdateStage.Should().BeTrue();
        result.Arguments.UpdateUI.Should().BeTrue();
        result.Arguments.UpdateAgent.Should().BeTrue();
        result.Arguments.UpdateServer.Should().BeTrue();
        result.Arguments.RestartUI.Should().BeTrue();
        result.Arguments.RestartAgent.Should().BeTrue();
        result.Arguments.RestartServer.Should().BeTrue();
        result.Arguments.ManifestPath.Should().Be("C:/temp/manifest.json");
        result.Arguments.SourcePath.Should().Be("C:/temp/source");
        result.Arguments.TargetPath.Should().Be("C:/temp/target");
        result.Arguments.SelfUpdateStagingPath.Should().Be("C:/temp/staging");
        result.Arguments.ContinueArguments.Should().Be("ZW1wdHk=");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenSelfUpdateApplyAndStageCombined_ShouldFailValidation()
    {
        var parser = new ArgumentParser();

        var result = parser.TryParse(new[] { "--self-update-stage", "--self-update-apply" });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("cannot be used together", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenNoActionSpecified_ShouldFailValidation()
    {
        var parser = new ArgumentParser();

        var result = parser.TryParse(new[] { "--manifest", "manifest.json" });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("At least one update or restart action", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("--manifest")]
    [InlineData("--source")]
    [InlineData("--target")]
    [InlineData("--self-update-staging")]
    [InlineData("--continue-args")]
    [Trait("Category", "Unit")]
    public void TryParse_WhenValueFlagMissingValue_ShouldFail(string flag)
    {
        var parser = new ArgumentParser();

        var result = parser.TryParse(new[] { "--update-ui", flag });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(flag, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenUnknownArgumentProvided_ShouldFail()
    {
        var parser = new ArgumentParser();

        var result = parser.TryParse(new[] { "--update-ui", "--unknown-flag" });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("Unknown argument", StringComparison.OrdinalIgnoreCase));
    }
}

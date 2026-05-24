using StorageWatch.Updater.Tests.Helpers;

namespace StorageWatch.Updater.Tests.Unit;

public class HashVerificationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeSha256_WhenInputBytesGiven_ShouldReturnExpectedLength()
    {
        var hash = HashTestUtilities.ComputeSha256(new byte[] { 1, 2, 3, 4 });

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeSha256_WhenSameInputGivenTwice_ShouldMatch()
    {
        var h1 = HashTestUtilities.ComputeSha256("same-content");
        var h2 = HashTestUtilities.ComputeSha256("same-content");

        h1.Should().Be(h2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeSha256_WhenDifferentInputGiven_ShouldNotMatch()
    {
        var h1 = HashTestUtilities.ComputeSha256("content-a");
        var h2 = HashTestUtilities.ComputeSha256("content-b");

        h1.Should().NotBe(h2);
    }
}

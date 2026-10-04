using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Text;

namespace GameDiscoveries.UnitTests.Text;

public sealed class SlugGeneratorTests
{
    [Theory]
    [InlineData("Fireboy and Watergirl Forest Temple", "fireboy-and-watergirl-forest-temple")]
    [InlineData("  Racing   Pro!! ", "racing-pro")]
    [InlineData("Éclair Game", "eclair-game")]
    public void FromTitle_generates_seo_slug(string title, string expected)
    {
        SlugGenerator.FromTitle(title).Should().Be(expected);
    }

    [Fact]
    public void DeterministicExternalId_is_stable()
    {
        var a = SlugGenerator.DeterministicExternalId("GameMonetize", "https://cdn.example/a");
        var b = SlugGenerator.DeterministicExternalId("GameMonetize", "https://cdn.example/a");
        var c = SlugGenerator.DeterministicExternalId("GameMonetize", "https://cdn.example/b");

        a.Should().Be(b);
        a.Should().NotBe(c);
        a.Should().HaveLength(32);
    }

    [Fact]
    public void EnsureUnique_appends_stable_suffix()
    {
        var id = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
        SlugGenerator.EnsureUnique("racing-pro", id).Should().Be("racing-pro-12345678");
    }
}

using FluentAssertions;
using GameDiscoveries.Modules.Catalog.Features.ListGames;
using GameDiscoveries.Modules.Discovery.Features.GetHomeDiscoveries;

namespace GameDiscoveries.UnitTests.Discovery;

public sealed class HomeShelvesTests
{
    private static GameSummaryResponse Game(string slug) =>
        new(Guid.NewGuid(), slug, slug, null, null, null, null, null, "web", true, null);

    [Fact]
    public void TopUp_keeps_primary_order_and_fills_without_duplicates()
    {
        var a = Game("a");
        var b = Game("b");
        var c = Game("c");
        var d = Game("d");

        var result = HomeShelves.TopUp([a, b], [b, c, d], 3);

        result.Select(g => g.Slug).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void TopUp_truncates_primary_to_limit()
    {
        var result = HomeShelves.TopUp([Game("a"), Game("b"), Game("c")], [Game("d")], 2);

        result.Select(g => g.Slug).Should().Equal("a", "b");
    }

    [Fact]
    public void TopUp_with_empty_fallback_returns_primary()
    {
        var result = HomeShelves.TopUp([Game("a")], [], 12);

        result.Should().ContainSingle().Which.Slug.Should().Be("a");
    }
}

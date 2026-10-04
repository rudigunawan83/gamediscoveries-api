using FluentAssertions;
using GameDiscoveries.Infrastructure.Providers.GameMonetize;
using GameDiscoveries.Infrastructure.Providers.GameMonetize.Models;

namespace GameDiscoveries.UnitTests.Providers;

public sealed class GameMonetizeMapperTests
{
    [Fact]
    public void Maps_feed_item_to_external_game()
    {
        var item = new GameMonetizeFeedItem
        {
            Id = "gm-1",
            Title = "Racing Pro",
            Description = "Fast cars",
            Thumb = "https://cdn.example/thumb.jpg",
            Url = "https://cdn.example/play",
            Category = "Racing",
            Tags = "cars, arcade",
            Width = "800",
            Height = "600",
            Company = "Studio X"
        };

        var game = GameMonetizeMapper.Map(item);

        game.Should().NotBeNull();
        game!.ProviderGameId.Should().Be("gm-1");
        game.Title.Should().Be("Racing Pro");
        game.Developer.Should().Be("Studio X");
        game.Categories.Should().ContainSingle("Racing");
        game.Tags.Should().BeEquivalentTo("cars", "arcade");
        game.Orientation.Should().Be("landscape");
        game.Width.Should().Be(800);
        game.Height.Should().Be(600);
        game.EmbedUrl.Should().Be("https://cdn.example/play");
    }

    [Fact]
    public void Returns_null_when_title_missing()
    {
        GameMonetizeMapper.Map(new GameMonetizeFeedItem { Id = "gm-1" }).Should().BeNull();
        GameMonetizeMapper.Map(null).Should().BeNull();
    }

    [Fact]
    public void Generates_deterministic_id_from_url_when_external_id_missing()
    {
        var item = new GameMonetizeFeedItem
        {
            Title = "Untitled Feed Game",
            Url = "https://cdn.example/games/abc"
        };

        var first = GameMonetizeMapper.Map(item);
        var second = GameMonetizeMapper.Map(item);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.ProviderGameId.Should().StartWith("url:");
        first.ProviderGameId.Should().Be(second!.ProviderGameId);
    }

    [Fact]
    public void Infers_portrait_orientation()
    {
        var item = new GameMonetizeFeedItem
        {
            Id = "gm-2",
            Title = "Portrait Game",
            Width = "400",
            Height = "800"
        };

        var game = GameMonetizeMapper.Map(item);
        game!.Orientation.Should().Be("portrait");
    }
}

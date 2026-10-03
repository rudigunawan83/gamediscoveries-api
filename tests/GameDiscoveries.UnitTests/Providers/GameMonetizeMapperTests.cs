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
            Height = "600"
        };

        var game = GameMonetizeMapper.Map(item);

        game.Should().NotBeNull();
        game!.ProviderGameId.Should().Be("gm-1");
        game.Title.Should().Be("Racing Pro");
        game.Categories.Should().ContainSingle("Racing");
        game.Tags.Should().BeEquivalentTo("cars", "arcade");
        game.Orientation.Should().Be("landscape");
    }

    [Fact]
    public void Returns_null_when_required_fields_missing()
    {
        GameMonetizeMapper.Map(new GameMonetizeFeedItem { Title = "Only title" }).Should().BeNull();
        GameMonetizeMapper.Map(null).Should().BeNull();
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

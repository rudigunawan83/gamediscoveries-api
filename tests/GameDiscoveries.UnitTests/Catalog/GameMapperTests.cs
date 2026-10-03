using FluentAssertions;
using GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

namespace GameDiscoveries.UnitTests.Catalog;

public sealed class GameMapperTests
{
    [Fact]
    public void Maps_game_row_to_response()
    {
        var now = DateTimeOffset.UtcNow;
        var row = new GameRow
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Slug = "example-game",
            Title = "Example Game",
            Description = "Demo",
            ThumbnailUrl = "https://cdn.example/thumb.jpg",
            CoverUrl = "https://cdn.example/cover.jpg",
            GameUrl = "https://cdn.example/play",
            Status = "published",
            MobileReady = true,
            Orientation = "landscape",
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = now
        };

        var response = GameMapper.ToResponse(row);

        response.Slug.Should().Be("example-game");
        response.Title.Should().Be("Example Game");
        response.MobileReady.Should().BeTrue();
        response.Status.Should().Be("published");
        response.Id.Should().Be(row.Id);
    }
}

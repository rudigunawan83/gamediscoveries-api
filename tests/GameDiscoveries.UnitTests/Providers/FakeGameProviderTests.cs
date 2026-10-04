using FluentAssertions;
using GameDiscoveries.Infrastructure.Providers.Abstractions;

namespace GameDiscoveries.UnitTests.Providers;

public sealed class FakeGameProviderTests
{
    [Fact]
    public async Task Fake_provider_supports_lookup_categories_and_play_url()
    {
        var provider = new FakeGameProvider(
        [
            new ExternalGame
            {
                ProviderGameId = "g1",
                Title = "Alpha",
                EmbedUrl = "https://example.test/play/g1",
                Categories = ["Action"],
                Tags = ["fast"]
            }
        ]);

        var games = await provider.GetGamesAsync();
        games.Should().ContainSingle();

        var game = await provider.GetGameAsync("g1");
        game!.Title.Should().Be("Alpha");

        var categories = await provider.GetCategoriesAsync();
        categories.Should().Contain("Action");

        var playUrl = await provider.GetGamePlayUrlAsync("g1");
        playUrl.Should().Be("https://example.test/play/g1");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameDiscoveries.IntegrationTests.Catalog;

[Collection(PostgresCollection.Name)]
public sealed class GetGameBySlugTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Get_game_by_slug_returns_game()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/games/example-game");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<GameResponse>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data!.Slug.Should().Be("example-game");
        payload.Data.Title.Should().Be("Example Game");
        payload.Error.Should().BeNull();
    }

    [Fact]
    public async Task Get_game_by_slug_returns_404_when_missing()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/games/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_game_by_slug_returns_422_for_invalid_slug()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/games/Invalid_Slug");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Health_live_returns_success()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

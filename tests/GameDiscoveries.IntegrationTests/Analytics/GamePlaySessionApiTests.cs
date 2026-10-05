using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Models;

namespace GameDiscoveries.IntegrationTests.Analytics;

[Collection(PostgresCollection.Name)]
public sealed class GamePlaySessionApiTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Start_heartbeat_pause_resume_end_flow()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var game = await client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/v1/games/example-game", JsonOptions);
        var gameId = game!.Data.GetProperty("id").GetGuid();
        var sessionId = Guid.NewGuid();
        var anonymousId = Guid.NewGuid();

        var start = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/sessions/start", new
        {
            sessionId,
            anonymousId,
            source = "WEB",
            platform = "WEB",
            deviceType = "DESKTOP"
        });
        start.StatusCode.Should().Be(HttpStatusCode.OK);

        var hb = await client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/heartbeat", new
        {
            anonymousId,
            visibilityState = "visible",
            isFocused = true,
            clientTimestamp = DateTimeOffset.UtcNow
        });
        hb.StatusCode.Should().Be(HttpStatusCode.OK);

        var pause = await client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/pause", new
        {
            anonymousId,
            reason = "TAB_HIDDEN"
        });
        pause.StatusCode.Should().Be(HttpStatusCode.OK);

        var resume = await client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/resume", new
        {
            anonymousId,
            reason = "APP_FOREGROUND"
        });
        resume.StatusCode.Should().Be(HttpStatusCode.OK);

        var end = await client.PostAsJsonAsync($"/api/v1/games/sessions/{sessionId}/end", new
        {
            anonymousId,
            reason = "USER_EXIT"
        });
        end.StatusCode.Should().Be(HttpStatusCode.OK);
        var ended = await end.Content.ReadFromJsonAsync<ApiResponse<GamePlaySessionResponse>>(JsonOptions);
        ended!.Data!.Status.Should().BeOneOf("ENDED", "INVALID");
    }

    [Fact]
    public async Task Duplicate_start_is_idempotent()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();
        var game = await client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/v1/games/example-game", JsonOptions);
        var gameId = game!.Data.GetProperty("id").GetGuid();
        var sessionId = Guid.NewGuid();
        var anonymousId = Guid.NewGuid();
        var body = new { sessionId, anonymousId, source = "WEB", platform = "WEB" };

        var first = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/sessions/start", body);
        var second = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/sessions/start", body);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invalid_game_returns_404()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/games/{Guid.NewGuid()}/sessions/start", new
        {
            sessionId = Guid.NewGuid(),
            anonymousId = Guid.NewGuid(),
            source = "WEB",
            platform = "WEB"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

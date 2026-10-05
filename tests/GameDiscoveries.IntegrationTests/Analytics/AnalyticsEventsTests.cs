using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameDiscoveries.IntegrationTests.Analytics;

[Collection(PostgresCollection.Name)]
public sealed class AnalyticsEventsTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Post_event_accepts_anonymous_page_view()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/v1/events", new
        {
            eventId,
            eventType = "PAGE_VIEW",
            anonymousId = Guid.NewGuid(),
            sessionId = Guid.NewGuid().ToString("N"),
            source = "WEB",
            platform = "WEB",
            pageUrl = "/",
            occurredAt = DateTimeOffset.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonOptions);
        payload!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Post_event_is_idempotent_for_duplicate_event_id()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();
        var eventId = Guid.NewGuid();
        var body = new
        {
            eventId,
            eventType = "PAGE_VIEW",
            anonymousId = Guid.NewGuid(),
            sessionId = "sess-dup",
            source = "WEB",
            platform = "WEB",
            pageUrl = "/dup",
            occurredAt = DateTimeOffset.UtcNow
        };

        var first = await client.PostAsJsonAsync("/api/v1/events", body);
        var second = await client.PostAsJsonAsync("/api/v1/events", body);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var secondPayload = await second.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonOptions);
        secondPayload!.Data.GetProperty("status").GetString().Should().Be("duplicate");
        secondPayload.Data.GetProperty("duplicates").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Post_event_rejects_unknown_type()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/events", new
        {
            eventId = Guid.NewGuid(),
            eventType = "NOT_REAL",
            anonymousId = Guid.NewGuid(),
            source = "WEB",
            platform = "WEB",
            occurredAt = DateTimeOffset.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_event_rejects_missing_game_for_game_view()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/events", new
        {
            eventId = Guid.NewGuid(),
            eventType = "GAME_VIEW",
            anonymousId = Guid.NewGuid(),
            source = "WEB",
            platform = "WEB",
            occurredAt = DateTimeOffset.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_event_returns_404_for_unknown_game()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/events", new
        {
            eventId = Guid.NewGuid(),
            eventType = "GAME_VIEW",
            anonymousId = Guid.NewGuid(),
            gameId = Guid.NewGuid(),
            source = "WEB",
            platform = "WEB",
            occurredAt = DateTimeOffset.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_batch_handles_partial_duplicates_and_rejects()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();
        var dupId = Guid.NewGuid();
        var anon = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/v1/events", new
        {
            eventId = dupId,
            eventType = "PAGE_VIEW",
            anonymousId = anon,
            pageUrl = "/a",
            source = "WEB",
            platform = "WEB",
            occurredAt = DateTimeOffset.UtcNow
        });

        var response = await client.PostAsJsonAsync("/api/v1/events/batch", new
        {
            events = new object[]
            {
                new
                {
                    eventId = dupId,
                    eventType = "PAGE_VIEW",
                    anonymousId = anon,
                    pageUrl = "/a",
                    source = "WEB",
                    platform = "WEB",
                    occurredAt = DateTimeOffset.UtcNow
                },
                new
                {
                    eventId = Guid.NewGuid(),
                    eventType = "PAGE_VIEW",
                    anonymousId = anon,
                    pageUrl = "/b",
                    source = "WEB",
                    platform = "WEB",
                    occurredAt = DateTimeOffset.UtcNow
                },
                new
                {
                    eventId = Guid.NewGuid(),
                    eventType = "NOT_REAL",
                    anonymousId = anon,
                    source = "WEB",
                    platform = "WEB",
                    occurredAt = DateTimeOffset.UtcNow
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AnalyticsBatchIngestResult>>(JsonOptions);
        payload!.Data.Should().NotBeNull();
        payload.Data!.Accepted.Should().Be(1);
        payload.Data.Duplicates.Should().Be(1);
        payload.Data.Rejected.Should().Be(1);
    }

    [Fact]
    public async Task Post_batch_rejects_oversized_batch()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();
        var anon = Guid.NewGuid();
        var events = Enumerable.Range(0, 101).Select(_ => new
        {
            eventId = Guid.NewGuid(),
            eventType = "PAGE_VIEW",
            anonymousId = anon,
            pageUrl = "/",
            source = "WEB",
            platform = "WEB",
            occurredAt = DateTimeOffset.UtcNow
        }).ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/events/batch", new { events });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Legacy_endpoint_still_works()
    {
        await using var factory = fixture.CreateFactory();
        var client = factory.CreateClient();

        // Seeded example game from migration 002
        var games = await client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/v1/games/example-game", JsonOptions);
        var gameId = games!.Data.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync("/api/v1/analytics/events", new
        {
            eventName = "game_viewed",
            gameId,
            sessionId = "legacy-session",
            anonymousId = Guid.NewGuid(),
            properties = new { source = "test" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}

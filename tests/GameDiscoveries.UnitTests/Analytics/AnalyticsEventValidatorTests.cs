using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Options;
using GameDiscoveries.Modules.Analytics.Services;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.UnitTests.Analytics;

public sealed class AnalyticsEventValidatorTests
{
    private readonly AnalyticsEventValidator _validator = new(
        Options.Create(new AnalyticsOptions
        {
            MaxMetadataSizeKb = 1,
            EnableAnonymousTracking = true
        }));

    [Fact]
    public void Normalizes_legacy_event_name_to_canonical()
    {
        var command = _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "game_viewed",
                Guid.NewGuid(),
                "session-1",
                Guid.NewGuid(),
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                new Dictionary<string, object?> { ["source"] = "home" },
                DateTimeOffset.UtcNow),
            authenticatedUserId: null,
            ipAddress: "1.2.3.4",
            userAgent: "test",
            receivedAt: DateTimeOffset.UtcNow);

        command.EventType.Should().Be(AnalyticsEventTypes.GameView);
        command.IpHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Rejects_spoofed_user_id()
    {
        var act = () => _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "LOGIN",
                Guid.NewGuid(),
                null,
                null,
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow,
                UserId: Guid.NewGuid()),
            authenticatedUserId: Guid.NewGuid(),
            ipAddress: null,
            userAgent: null,
            receivedAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Requires_anonymous_id_when_unauthenticated()
    {
        var act = () => _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "PAGE_VIEW",
                null,
                null,
                null,
                "WEB",
                "WEB",
                null,
                null,
                "/",
                null,
                null,
                DateTimeOffset.UtcNow),
            authenticatedUserId: null,
            ipAddress: null,
            userAgent: null,
            receivedAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Requires_game_id_for_game_view()
    {
        var act = () => _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "GAME_VIEW",
                Guid.NewGuid(),
                null,
                null,
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow),
            authenticatedUserId: null,
            ipAddress: null,
            userAgent: null,
            receivedAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Requires_session_id_for_game_session_end()
    {
        var act = () => _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "GAME_SESSION_END",
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow),
            authenticatedUserId: null,
            ipAddress: null,
            userAgent: null,
            receivedAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Rejects_unknown_event_type()
    {
        var act = () => _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "NOT_A_REAL_EVENT",
                Guid.NewGuid(),
                null,
                null,
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow),
            authenticatedUserId: null,
            ipAddress: null,
            userAgent: null,
            receivedAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Rejects_oversized_metadata()
    {
        var huge = new string('x', 2048);
        var act = () => _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "PAGE_VIEW",
                Guid.NewGuid(),
                null,
                null,
                "WEB",
                "WEB",
                null,
                null,
                "/",
                null,
                new Dictionary<string, object?> { ["blob"] = huge },
                DateTimeOffset.UtcNow),
            authenticatedUserId: null,
            ipAddress: null,
            userAgent: null,
            receivedAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Authenticated_event_uses_token_user_id()
    {
        var userId = Guid.NewGuid();
        var command = _validator.ValidateAndNormalize(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "LOGIN",
                Guid.NewGuid(),
                "s1",
                null,
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow),
            authenticatedUserId: userId,
            ipAddress: null,
            userAgent: null,
            receivedAt: DateTimeOffset.UtcNow);

        command.UserId.Should().Be(userId);
    }
}

public sealed class AnalyticsEventTypesTests
{
    [Theory]
    [InlineData("GAME_START", "GAME_START")]
    [InlineData("game_started", "GAME_START")]
    [InlineData("game_exit", "GAME_SESSION_END")]
    [InlineData("page_view", "PAGE_VIEW")]
    public void TryNormalize_maps_aliases(string raw, string expected)
    {
        AnalyticsEventTypes.TryNormalize(raw, out var canonical).Should().BeTrue();
        canonical.Should().Be(expected);
    }
}

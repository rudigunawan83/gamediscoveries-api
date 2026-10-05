using FluentAssertions;
using GameDiscoveries.Modules.Analytics.Domain;

namespace GameDiscoveries.UnitTests.Analytics;

public sealed class GamePlayActiveTimeCalculatorTests
{
    [Fact]
    public void Credits_heartbeat_within_timeout()
    {
        var previous = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
        var now = DateTimeOffset.Parse("2026-10-05T10:00:30Z");

        var credit = GamePlayActiveTimeCalculator.CreditHeartbeatMs(previous, now, 90, 60);

        credit.Should().Be(30_000);
    }

    [Fact]
    public void Does_not_credit_gap_beyond_timeout()
    {
        var previous = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
        var now = DateTimeOffset.Parse("2026-10-05T10:02:00Z");

        var credit = GamePlayActiveTimeCalculator.CreditHeartbeatMs(previous, now, 90, 60);

        credit.Should().Be(0);
    }

    [Fact]
    public void Caps_credit_to_max_heartbeat_seconds()
    {
        var previous = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
        var now = DateTimeOffset.Parse("2026-10-05T10:01:20Z");

        var credit = GamePlayActiveTimeCalculator.CreditHeartbeatMs(previous, now, 90, 60);

        credit.Should().Be(60_000);
    }

    [Fact]
    public void Scenario_pause_excluded_from_active_time()
    {
        // Simulate 3 min active via 30s heartbeats, 2 min pause (no credit), 3 min active again.
        long accumulated = 0;
        var cursor = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
        var start = cursor;
        for (var i = 0; i < 6; i++)
        {
            var next = cursor.AddSeconds(30);
            accumulated += GamePlayActiveTimeCalculator.CreditHeartbeatMs(cursor, next, 90, 60);
            cursor = next;
        }

        // pause 2 minutes — gap > timeout, no credit
        var afterPause = cursor.AddMinutes(2);
        accumulated += GamePlayActiveTimeCalculator.CreditHeartbeatMs(cursor, afterPause, 90, 60);
        cursor = afterPause;

        for (var i = 0; i < 6; i++)
        {
            var next = cursor.AddSeconds(30);
            accumulated += GamePlayActiveTimeCalculator.CreditHeartbeatMs(cursor, next, 90, 60);
            cursor = next;
        }

        var active = GamePlayActiveTimeCalculator.ToActiveSeconds(accumulated);
        active.Should().Be(360);

        var duration = GamePlayActiveTimeCalculator.DurationSeconds(start, cursor);
        duration.Should().Be(480);
    }

    [Theory]
    [InlineData(10, 10, false, "SESSION_TOO_SHORT")]
    [InlineData(60, 60, true, null)]
    [InlineData(100, 20_000, false, "SESSION_TOO_LONG")]
    public void EvaluateValidity(int active, int duration, bool expectedValid, string? reason)
    {
        var (isValid, invalidReason) = GamePlayActiveTimeCalculator.EvaluateValidity(
            active,
            duration,
            minimumValidActiveSeconds: 30,
            maximumSessionDurationSeconds: 14_400);

        isValid.Should().Be(expectedValid);
        invalidReason.Should().Be(reason);
    }
}

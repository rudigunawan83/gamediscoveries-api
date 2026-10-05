using FluentAssertions;
using GameDiscoveries.Modules.Streaks.Domain;
using GameDiscoveries.Modules.Streaks.Services;

namespace GameDiscoveries.UnitTests.Streaks;

public sealed class StreakEngineTests
{
    [Fact]
    public void First_activity_starts_streak_at_1()
    {
        var result = StreakEngine.ApplyActivity(0, 0, null, null, 0, true, new DateOnly(2026, 10, 5));
        result.CurrentStreak.Should().Be(1);
        result.LongestStreak.Should().Be(1);
        result.EventType.Should().Be(StreakEventTypes.Started);
        result.SameDayNoOp.Should().BeFalse();
    }

    [Fact]
    public void Same_day_does_not_increase()
    {
        var day = new DateOnly(2026, 10, 5);
        var result = StreakEngine.ApplyActivity(1, 1, day, day, 0, true, day);
        result.CurrentStreak.Should().Be(1);
        result.SameDayNoOp.Should().BeTrue();
        result.EventType.Should().BeNull();
    }

    [Fact]
    public void Consecutive_day_increments()
    {
        var mon = new DateOnly(2026, 10, 5);
        var tue = new DateOnly(2026, 10, 6);
        var result = StreakEngine.ApplyActivity(1, 1, mon, mon, 0, true, tue);
        result.CurrentStreak.Should().Be(2);
        result.EventType.Should().Be(StreakEventTypes.Continued);
    }

    [Fact]
    public void Missed_day_without_freeze_breaks_and_restarts()
    {
        var mon = new DateOnly(2026, 10, 5);
        var tue = new DateOnly(2026, 10, 6);
        var thu = new DateOnly(2026, 10, 8);
        // Last activity Tuesday, returns Thursday → gap 2 → break
        var result = StreakEngine.ApplyActivity(2, 2, mon, tue, 0, true, thu);
        result.CurrentStreak.Should().Be(1);
        result.LongestStreak.Should().Be(2);
        result.EventType.Should().Be(StreakEventTypes.Broken);
    }

    [Fact]
    public void Freeze_protects_one_missed_day()
    {
        var mon = new DateOnly(2026, 10, 5);
        var tue = new DateOnly(2026, 10, 6);
        var thu = new DateOnly(2026, 10, 8);
        // Missed Wednesday (gap 2), one freeze available
        var result = StreakEngine.ApplyActivity(2, 2, mon, tue, freezeCount: 1, freezeEnabled: true, thu);
        result.CurrentStreak.Should().Be(3);
        result.FreezeCount.Should().Be(0);
        result.FreezeConsumed.Should().BeTrue();
        result.EventType.Should().Be(StreakEventTypes.Frozen);
    }

    [Fact]
    public void Longest_streak_updates()
    {
        var result = StreakEngine.ApplyActivity(7, 7, new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4), 0, true, new DateOnly(2026, 10, 5));
        result.CurrentStreak.Should().Be(8);
        result.LongestStreak.Should().Be(8);
    }

    [Theory]
    [InlineData(7, "2026-10-04", "2026-10-05", StreakStatuses.AtRisk)]
    [InlineData(7, "2026-10-05", "2026-10-05", StreakStatuses.Active)]
    [InlineData(0, null, "2026-10-05", StreakStatuses.Broken)]
    [InlineData(3, "2026-10-01", "2026-10-05", StreakStatuses.Broken)]
    public void Display_status_resolution(int current, string? last, string today, string expected)
    {
        DateOnly? lastDate = last is null ? null : DateOnly.Parse(last);
        var status = StreakEngine.ResolveDisplayStatus(current, lastDate, DateOnly.Parse(today), StreakStatuses.Active);
        status.Should().Be(expected);
    }
}

public sealed class StreakDateCalculatorTests
{
    private static readonly TimeZoneInfo Jakarta =
        TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Jakarta", out var tz)
            ? tz
            : TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    [Fact]
    public void Local_dates_split_across_jakarta_midnight()
    {
        // 16:59 UTC = 23:59 Jakarta Oct 5
        var before = new DateTimeOffset(2026, 10, 5, 16, 59, 0, TimeSpan.Zero);
        // 17:01 UTC = 00:01 Jakarta Oct 6
        var after = new DateTimeOffset(2026, 10, 5, 17, 1, 0, TimeSpan.Zero);

        StreakDateCalculator.ToLocalDate(before, Jakarta).Should().Be(new DateOnly(2026, 10, 5));
        StreakDateCalculator.ToLocalDate(after, Jakarta).Should().Be(new DateOnly(2026, 10, 6));
    }
}

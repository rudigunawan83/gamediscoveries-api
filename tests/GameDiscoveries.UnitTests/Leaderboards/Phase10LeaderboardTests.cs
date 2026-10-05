using FluentAssertions;
using GameDiscoveries.Modules.Leaderboards.Domain;
using GameDiscoveries.Modules.Leaderboards.Options;
using GameDiscoveries.Modules.Leaderboards.Services;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.UnitTests.Leaderboards;

public sealed class Phase10LeaderboardTests
{
    [Fact]
    public void Weekly_period_starts_monday_jakarta()
    {
        // Wednesday 2026-10-07 10:00 UTC ≈ afternoon Jakarta
        var now = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
        var (start, end, code) = LeaderboardPeriodCalculator.CurrentWeekly(now, "Asia/Jakarta");
        start.Should().BeBefore(end);
        code.Should().StartWith("WEEKLY_");
        // Local Monday 00:00 Asia/Jakarta
        var tz = LeaderboardPeriodCalculator.ResolveTimezone("Asia/Jakarta");
        var localStart = TimeZoneInfo.ConvertTime(start, tz);
        localStart.DayOfWeek.Should().Be(DayOfWeek.Monday);
        localStart.Hour.Should().Be(0);
    }

    [Fact]
    public void Monthly_period_handles_month_length()
    {
        var feb = new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.Zero);
        var (start, end, code) = LeaderboardPeriodCalculator.CurrentMonthly(feb, "Asia/Jakarta");
        code.Should().Be("MONTHLY_2026-02");
        (end - start).TotalDays.Should().BeApproximately(28, 0.1);
    }

    [Fact]
    public void Eligibility_excludes_admin_and_competition_reward_by_default()
    {
        var svc = new LeaderboardEligibilityService(Options.Create(new LeaderboardOptions()));
        svc.IsXpTransactionEligible("SESSION_VALID", "GAME_SESSION_END", 50).Should().BeTrue();
        svc.IsXpTransactionEligible("ADMIN_ADJUSTMENT", "ADMIN_ADJUSTMENT", 1000).Should().BeFalse();
        svc.IsXpTransactionEligible("COMPETITION_REWARD", "COMPETITION_REWARD", 1000).Should().BeFalse();
        svc.IsXpTransactionEligible("XP_REVERSAL", "XP_REVERSAL", -50).Should().BeTrue();
    }

    [Fact]
    public void Eligibility_can_include_admin_when_configured()
    {
        var svc = new LeaderboardEligibilityService(Options.Create(new LeaderboardOptions
        {
            IncludeAdminAdjustmentInLeaderboard = true
        }));
        svc.IsXpTransactionEligible("ADMIN_ADJUSTMENT", "ADMIN_ADJUSTMENT", 100).Should().BeTrue();
    }

    [Fact]
    public void Percentile_and_rank_movement()
    {
        LeaderboardRankHelper.Percentile(1, 100).Should().Be(100.0);
        LeaderboardRankHelper.Percentile(100, 10000).Should().Be(99.0);
        LeaderboardRankHelper.ToMovement(null, 12, 0).Should().Be("NEW");
        LeaderboardRankHelper.ToMovement(10, 7, 3).Should().Be("UP_3");
        LeaderboardRankHelper.ToMovement(5, 8, -3).Should().Be("DOWN_3");
        LeaderboardRankHelper.ToMovement(4, 4, 0).Should().Be("SAME");
    }

    [Fact]
    public void Tie_break_order_is_score_then_reached_at()
    {
        var a = (Score: 100L, Reached: DateTimeOffset.UtcNow.AddMinutes(-10), Sessions: 1, UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var b = (Score: 100L, Reached: DateTimeOffset.UtcNow.AddMinutes(-5), Sessions: 5, UserId: Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var ordered = new[] { b, a }
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Reached)
            .ThenByDescending(x => x.Sessions)
            .ThenBy(x => x.UserId)
            .ToList();
        ordered[0].UserId.Should().Be(a.UserId);
    }

    [Fact]
    public void Global_scope_provider_exists()
    {
        new GlobalScopeProvider().ScopeType.Should().Be("GLOBAL");
    }
}

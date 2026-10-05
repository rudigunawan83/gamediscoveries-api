using FluentAssertions;
using GameDiscoveries.Modules.Missions.Domain;
using GameDiscoveries.Modules.Missions.Models;
using GameDiscoveries.Modules.Missions.Services;

namespace GameDiscoveries.UnitTests.Missions;

public sealed class MissionPeriodCalculatorTests
{
    private static readonly TimeZoneInfo Jakarta =
        TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Jakarta", out var tz)
            ? tz
            : TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    [Fact]
    public void Daily_period_is_local_midnight_to_end_of_day()
    {
        // 2026-10-05 10:00 UTC = 17:00 Jakarta
        var utc = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);
        var period = MissionPeriodCalculator.Daily(utc, Jakarta);

        var startLocal = TimeZoneInfo.ConvertTime(period.Start, Jakarta);
        var endLocal = TimeZoneInfo.ConvertTime(period.End, Jakarta);

        startLocal.Date.Should().Be(new DateTime(2026, 10, 5));
        startLocal.Hour.Should().Be(0);
        endLocal.Date.Should().Be(new DateTime(2026, 10, 5));
        endLocal.Hour.Should().Be(23);
        endLocal.Minute.Should().Be(59);
    }

    [Fact]
    public void Weekly_period_starts_monday_jakarta()
    {
        // Wednesday 2026-10-07
        var utc = new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero);
        var period = MissionPeriodCalculator.Weekly(utc, Jakarta);
        var startLocal = TimeZoneInfo.ConvertTime(period.Start, Jakarta);
        var endLocal = TimeZoneInfo.ConvertTime(period.End, Jakarta);

        startLocal.DayOfWeek.Should().Be(DayOfWeek.Monday);
        startLocal.Date.Should().Be(new DateTime(2026, 10, 5));
        endLocal.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        endLocal.Date.Should().Be(new DateTime(2026, 10, 11));
    }

    [Fact]
    public void Daily_boundary_before_jakarta_midnight_stays_previous_day()
    {
        // 2026-10-04 17:30 UTC = 2026-10-05 00:30 Jakarta → daily is Oct 5
        var afterMidnight = new DateTimeOffset(2026, 10, 4, 17, 30, 0, TimeSpan.Zero);
        var periodAfter = MissionPeriodCalculator.Daily(afterMidnight, Jakarta);
        TimeZoneInfo.ConvertTime(periodAfter.Start, Jakarta).Date.Should().Be(new DateTime(2026, 10, 5));

        // 2026-10-04 16:30 UTC = 2026-10-04 23:30 Jakarta → daily is Oct 4
        var beforeMidnight = new DateTimeOffset(2026, 10, 4, 16, 30, 0, TimeSpan.Zero);
        var periodBefore = MissionPeriodCalculator.Daily(beforeMidnight, Jakarta);
        TimeZoneInfo.ConvertTime(periodBefore.Start, Jakarta).Date.Should().Be(new DateTime(2026, 10, 4));
    }
}

public sealed class DefaultMissionSelectionStrategyTests
{
    private static MissionTemplateEntity T(
        string code,
        string difficulty,
        string type = MissionTypes.Daily,
        int sort = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Type = type,
            Title = code,
            Description = code,
            RequirementType = MissionRequirementTypes.UniqueGamesPlayed,
            TargetValue = 1,
            RewardXp = 50,
            Difficulty = difficulty,
            IsActive = true,
            SortOrder = sort
        };

    [Fact]
    public void Selects_easy_medium_and_exploration_for_daily()
    {
        var strategy = new DefaultMissionSelectionStrategy();
        var templates = new List<MissionTemplateEntity>
        {
            T(MissionCodes.FavoriteAGame, MissionDifficulties.Easy, sort: 1),
            T(MissionCodes.Play2Games, MissionDifficulties.Medium, sort: 2),
            T(MissionCodes.DiscoverNewGame, MissionDifficulties.Medium, sort: 3),
            T(MissionCodes.ExploreNewGenre, MissionDifficulties.Hard, sort: 4),
            T(MissionCodes.Play10Minutes, MissionDifficulties.Medium, sort: 5)
        };

        var selected = strategy.SelectDaily(templates, isNewUser: false, count: 3);
        selected.Should().HaveCount(3);
        selected.Select(s => s.Code).Should().Contain(MissionCodes.FavoriteAGame);
        selected.Select(s => s.Code).Should().Contain(MissionCodes.Play2Games);
        selected.Should().Contain(s => s.Code.Contains("DISCOVER") || s.Code.Contains("EXPLORE"));
    }

    [Fact]
    public void New_user_prefers_easier_missions()
    {
        var strategy = new DefaultMissionSelectionStrategy();
        var templates = new List<MissionTemplateEntity>
        {
            T(MissionCodes.FavoriteAGame, MissionDifficulties.Easy),
            T(MissionCodes.DiscoverNewGame, MissionDifficulties.Medium),
            T(MissionCodes.Play2Games, MissionDifficulties.Medium),
            T(MissionCodes.Discover10Games, MissionDifficulties.Hard, MissionTypes.Daily)
        };

        var selected = strategy.SelectDaily(templates, isNewUser: true, count: 3);
        selected.Should().HaveCount(3);
        selected.Select(s => s.Code).Should().Contain(MissionCodes.FavoriteAGame);
        selected.Select(s => s.Code).Should().NotContain(MissionCodes.Discover10Games);
    }

    [Fact]
    public void Weekly_selects_medium_and_hard()
    {
        var strategy = new DefaultMissionSelectionStrategy();
        var templates = new List<MissionTemplateEntity>
        {
            T(MissionCodes.Play5Games, MissionDifficulties.Medium, MissionTypes.Weekly),
            T(MissionCodes.Play5Days, MissionDifficulties.Hard, MissionTypes.Weekly),
            T(MissionCodes.Play60Minutes, MissionDifficulties.Medium, MissionTypes.Weekly)
        };

        var selected = strategy.SelectWeekly(templates, count: 2);
        selected.Should().HaveCount(2);
        selected.Select(s => s.Difficulty).Should().Contain(MissionDifficulties.Medium);
        selected.Select(s => s.Difficulty).Should().Contain(MissionDifficulties.Hard);
    }

    [Fact]
    public void Falls_back_when_difficulty_missing()
    {
        var strategy = new DefaultMissionSelectionStrategy();
        var templates = new List<MissionTemplateEntity>
        {
            T("ONLY_HARD_1", MissionDifficulties.Hard),
            T("ONLY_HARD_2", MissionDifficulties.Hard),
            T("ONLY_HARD_3", MissionDifficulties.Hard)
        };

        var selected = strategy.SelectDaily(templates, isNewUser: false, count: 3);
        selected.Should().HaveCount(3);
    }
}

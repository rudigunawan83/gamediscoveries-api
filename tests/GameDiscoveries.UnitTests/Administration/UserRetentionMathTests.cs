using FluentAssertions;
using GameDiscoveries.Modules.Administration.Features.Monitoring;
using Xunit;

namespace GameDiscoveries.UnitTests.Administration;

public sealed class UserRetentionMathTests
{
    private static readonly DateTimeOffset Sunday = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentWeek = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Week_starts_on_monday_utc()
    {
        UserRetentionMath.WeekStart(Sunday).Should().Be(CurrentWeek);
        UserRetentionMath.WeekStart(CurrentWeek).Should().Be(CurrentWeek);
        UserRetentionMath.WeekStart(new DateTimeOffset(2026, 10, 6, 1, 0, 0, TimeSpan.FromHours(7)))
            .Should().Be(CurrentWeek);
    }

    [Fact]
    public void Cohorts_cover_the_last_eight_weeks_oldest_first()
    {
        var cohorts = UserRetentionMath.Cohorts(Sunday, []);

        cohorts.Should().HaveCount(8);
        cohorts[0].WeekStart.Should().Be(CurrentWeek.AddDays(-49));
        cohorts[^1].WeekStart.Should().Be(CurrentWeek);
        cohorts.Should().OnlyContain(cohort => cohort.Retention.Count == 9);
    }

    [Fact]
    public void Retention_is_share_of_week_zero_and_null_for_weeks_not_started()
    {
        var start = CurrentWeek.AddDays(-14);
        var cohorts = UserRetentionMath.Cohorts(Sunday,
        [
            new UserCohortCell(start, 0, 40),
            new UserCohortCell(start, 1, 10),
            new UserCohortCell(start, 2, 4)
        ]);

        var cohort = cohorts.Single(item => item.WeekStart == start);
        cohort.Users.Should().Be(40);
        cohort.Retention.Take(3).Should().Equal(100, 25, 10);
        cohort.Retention.Skip(3).Should().OnlyContain(value => value == null);
    }

    [Fact]
    public void Empty_cohort_has_no_percentages()
    {
        var cohorts = UserRetentionMath.Cohorts(Sunday, []);

        cohorts[^1].Users.Should().Be(0);
        cohorts[^1].Retention.Should().OnlyContain(value => value == null);
    }

    [Fact]
    public void Heatmap_fills_every_weekday_and_hour()
    {
        var cells = UserRetentionMath.Heatmap([new UserHeatmapCell(3, 14, 9)]);

        cells.Should().HaveCount(7 * 24);
        cells.Single(cell => cell.Day == 3 && cell.Hour == 14).Users.Should().Be(9);
        cells.Where(cell => cell.Day != 3 || cell.Hour != 14).Should().OnlyContain(cell => cell.Users == 0);
    }

    [Fact]
    public void Percent_and_ratio_are_null_without_a_base()
    {
        UserRetentionMath.Percent(3, 0).Should().BeNull();
        UserRetentionMath.Percent(1, 3).Should().Be(33.3);
        UserRetentionMath.Ratio(5, 0).Should().BeNull();
        UserRetentionMath.Ratio(7, 2).Should().Be(3.5);
    }
}

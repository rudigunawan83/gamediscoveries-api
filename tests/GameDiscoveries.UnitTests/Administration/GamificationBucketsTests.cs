using FluentAssertions;
using GameDiscoveries.Modules.Administration.Features.Monitoring;
using Xunit;

namespace GameDiscoveries.UnitTests.Administration;

public sealed class GamificationBucketsTests
{
    [Theory]
    [InlineData(1, "1_5")]
    [InlineData(5, "1_5")]
    [InlineData(6, "6_10")]
    [InlineData(15, "11_15")]
    [InlineData(20, "16_20")]
    [InlineData(30, "21_30")]
    [InlineData(50, "31_50")]
    [InlineData(51, "51_plus")]
    public void Levels_map_to_display_ranges(int level, string key)
    {
        GamificationBuckets.Level(level).Should().Be(key);
    }

    [Theory]
    [InlineData(1, "1_3")]
    [InlineData(4, "4_7")]
    [InlineData(7, "4_7")]
    [InlineData(14, "8_14")]
    [InlineData(30, "15_30")]
    [InlineData(31, "31_plus")]
    public void Streaks_map_to_display_ranges(int days, string key)
    {
        GamificationBuckets.Streak(days).Should().Be(key);
    }

    [Fact]
    public void Ordered_sums_rows_per_key_and_keeps_missing_keys_at_zero()
    {
        var shares = GamificationBuckets.Ordered(
            GamificationBuckets.StreakKeys,
            [("4_7", 2), ("1_3", 5), ("4_7", 3)]);

        shares.Select(share => share.Key).Should().Equal("1_3", "4_7", "8_14", "15_30", "31_plus");
        shares.Select(share => share.Count).Should().Equal(5, 5, 0, 0, 0);
    }

    [Fact]
    public void Unknown_keys_are_not_added()
    {
        GamificationBuckets.Ordered(GamificationBuckets.Difficulties, [("MYTHIC", 4)])
            .Should().OnlyContain(share => share.Count == 0);
    }
}

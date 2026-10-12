using FluentAssertions;
using GameDiscoveries.Modules.Administration.Features.Monitoring;
using Xunit;

namespace GameDiscoveries.UnitTests.Administration;

public sealed class PlaytimeBucketsTests
{
    [Fact]
    public void Maps_counts_to_buckets_in_display_order()
    {
        var buckets = PlaytimeBuckets.From([4, 3, 2, 1, 0, 7]);

        buckets.Select(bucket => bucket.Key).Should().Equal("lt_1m", "1_5m", "5_10m", "10_30m", "30_60m", "gt_60m");
        buckets.Select(bucket => bucket.Sessions).Should().Equal(4, 3, 2, 1, 0, 7);
    }

    [Fact]
    public void Missing_counts_are_zero()
    {
        PlaytimeBuckets.From([5]).Select(bucket => bucket.Sessions).Should().Equal(5, 0, 0, 0, 0, 0);
    }
}

public sealed class LaunchRatesTests
{
    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(3, 0, 100.0)]
    [InlineData(2, 1, 66.7)]
    [InlineData(0, 4, 0.0)]
    public void Success_percent_only_counts_reported_outcomes(long loaded, long failed, double? expected) =>
        LaunchRates.SuccessPercent(loaded, failed).Should().Be(expected);
}

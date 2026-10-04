using FluentAssertions;
using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Services;

namespace GameDiscoveries.UnitTests.Recommendation;

public sealed class TimeDecayTests
{
    private readonly TimeDecayOptions _options = new();

    [Fact]
    public void Today_Is_Full_Weight()
    {
        var now = DateTimeOffset.Parse("2026-10-04T12:00:00Z");
        TimeDecay.Compute(now, now, _options).Should().Be(1.0);
    }

    [Fact]
    public void Older_Signals_Decay()
    {
        var now = DateTimeOffset.Parse("2026-10-04T12:00:00Z");
        var d7 = TimeDecay.Compute(now.AddDays(-7), now, _options);
        var d60 = TimeDecay.Compute(now.AddDays(-60), now, _options);
        d7.Should().BeApproximately(0.65, 0.05);
        d60.Should().BeLessThanOrEqualTo(0.15);
        d7.Should().BeGreaterThan(d60);
    }

    [Theory]
    [InlineData(1, 0.3)]
    [InlineData(2, 0.5)]
    [InlineData(3, 0.7)]
    [InlineData(5, 1.0)]
    [InlineData(20, 1.0)]
    public void Interaction_Confidence_Caps(int count, double expected)
    {
        TimeDecay.InteractionConfidence(count).Should().Be(expected);
    }
}

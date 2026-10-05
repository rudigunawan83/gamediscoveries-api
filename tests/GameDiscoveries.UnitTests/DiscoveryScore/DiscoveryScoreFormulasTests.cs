using FluentAssertions;
using GameDiscoveries.Modules.DiscoveryScore.Domain;

namespace GameDiscoveries.UnitTests.DiscoveryScore;

public sealed class DiscoveryScoreFormulasTests
{
    [Fact]
    public void Weights_must_sum_to_one()
    {
        DiscoveryScoreFormulas.ValidateWeights(0.2, 0.25, 0.15, 0.2, 0.1, 0.1).Should().BeTrue();
        DiscoveryScoreFormulas.ValidateWeights(0.5, 0.5, 0.1).Should().BeFalse();
    }

    [Fact]
    public void Bayesian_rating_shrinks_low_sample_towards_global_mean()
    {
        var score = DiscoveryScoreFormulas.BayesianRating(5, 1, 20, 4.0);
        score.Should().BeApproximately(4.0476, 0.01);
    }

    [Fact]
    public void Bayesian_rating_trusts_high_sample()
    {
        var score = DiscoveryScoreFormulas.BayesianRating(4.6, 2000, 20, 4.0);
        score.Should().BeApproximately(4.594, 0.01);
    }

    [Fact]
    public void Freshness_decays_with_age()
    {
        var fresh = DiscoveryScoreFormulas.FreshnessScore(0, 30);
        var aged = DiscoveryScoreFormulas.FreshnessScore(30, 30);
        fresh.Should().BeApproximately(100, 0.01);
        aged.Should().BeApproximately(100 / Math.E, 0.5);
        aged.Should().BeLessThan(fresh);
    }

    [Fact]
    public void Momentum_growth_is_smoothed_and_bounded()
    {
        var up = MetricNormalization.NormalizeGrowth(2000, 1000, 10);
        var flat = MetricNormalization.NormalizeGrowth(2000, 2000, 10);
        var down = MetricNormalization.NormalizeGrowth(1000, 2000, 10);
        var tinySpike = MetricNormalization.NormalizeGrowth(10, 1, 10);

        up.Should().BeGreaterThan(flat);
        flat.Should().BeApproximately(50, 1);
        down.Should().BeLessThan(flat);
        tinySpike.Should().BeLessThan(90);
    }

    [Fact]
    public void Final_discovery_score_uses_weights()
    {
        var score = DiscoveryScoreFormulas.DiscoveryScore(
            82, 91, 88, 94, 89, 100,
            0.20, 0.25, 0.15, 0.20, 0.10, 0.10);

        score.Should().BeApproximately(90.05, 0.05);
    }

    [Fact]
    public void Trending_score_emphasizes_recent_activity_and_momentum()
    {
        var score = DiscoveryScoreFormulas.TrendingScore(90, 95, 80, 70, 60, 0.30, 0.30, 0.20, 0.15, 0.05);
        score.Should().BeGreaterThan(80);
        score.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void Trend_state_resolution()
    {
        DiscoveryScoreFormulas.ResolveTrendState(40, 50, false, 25, -20)
            .Should().Be(DiscoveryTrendStates.Rising);
        DiscoveryScoreFormulas.ResolveTrendState(-30, 50, false, 25, -20)
            .Should().Be(DiscoveryTrendStates.Declining);
        DiscoveryScoreFormulas.ResolveTrendState(0, 80, false, 25, -20)
            .Should().Be(DiscoveryTrendStates.Hot);
        DiscoveryScoreFormulas.ResolveTrendState(0, 50, true, 25, -20)
            .Should().Be(DiscoveryTrendStates.New);
    }

    [Fact]
    public void Zero_data_normalization_is_safe()
    {
        MetricNormalization.NormalizeLog1pMinMax([], 0).Should().Be(0);
        MetricNormalization.NormalizePercentile([0, 0, 0], 0).Should().Be(100);
        MetricNormalization.Clamp(-5).Should().Be(0);
        MetricNormalization.Clamp(150).Should().Be(100);
    }

    [Fact]
    public void Popularity_and_engagement_formulas_are_bounded()
    {
        DiscoveryScoreFormulas.PopularityScore(100, 100, 100, 100).Should().Be(100);
        DiscoveryScoreFormulas.EngagementScore(10, 10, 10, 10, 10).Should().Be(10);
        DiscoveryScoreFormulas.QualityScore(4.5, 80, 60).Should().BeInRange(0, 100);
    }

    [Fact]
    public void Impact_labels()
    {
        DiscoveryScoreFormulas.ImpactLabel(90).Should().Be("HIGH");
        DiscoveryScoreFormulas.ImpactLabel(50).Should().Be("MEDIUM");
        DiscoveryScoreFormulas.ImpactLabel(10).Should().Be("LOW");
    }
}

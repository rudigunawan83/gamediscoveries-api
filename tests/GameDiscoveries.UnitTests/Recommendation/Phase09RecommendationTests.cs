using FluentAssertions;
using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.UnitTests.Recommendation;

public sealed class Phase09RecommendationTests
{
    [Fact]
    public void Jaccard_handles_empty_and_overlap()
    {
        GameSimilarity.Jaccard([], ["a"]).Should().Be(0);
        GameSimilarity.Jaccard(["puzzle", "casual"], ["puzzle", "strategy"]).Should().BeApproximately(1d / 3d, 0.01);
    }

    [Fact]
    public void Rating_preference_is_centered_at_three()
    {
        PreferenceSignals.RatingPreference(5).Should().Be(1);
        PreferenceSignals.RatingPreference(4).Should().Be(0.5);
        PreferenceSignals.RatingPreference(3).Should().Be(0);
        PreferenceSignals.RatingPreference(1).Should().Be(-1);
    }

    [Fact]
    public void Recency_decay_prefers_recent_signals()
    {
        var now = DateTimeOffset.UtcNow;
        var today = PreferenceSignals.RecencyDecay(now, now, 30);
        var month = PreferenceSignals.RecencyDecay(now.AddDays(-30), now, 30);
        today.Should().BeGreaterThan(month);
        today.Should().BeApproximately(1, 0.01);
    }

    [Fact]
    public void Profile_levels_progress()
    {
        PreferenceSignals.ResolveProfileLevel(0).Should().Be(0);
        PreferenceSignals.ResolveProfileLevel(2).Should().Be(1);
        PreferenceSignals.ResolveProfileLevel(5).Should().Be(2);
        PreferenceSignals.ResolveProfileLevel(20).Should().Be(3);
        PreferenceSignals.ResolveProfileLevel(80).Should().Be(4);
    }

    [Fact]
    public void Repeat_play_penalty_is_softer_for_favorites()
    {
        var now = DateTimeOffset.UtcNow;
        var recent = PreferenceSignals.RepeatPlayPenalty(now.AddHours(-2), now, false);
        var favorite = PreferenceSignals.RepeatPlayPenalty(now.AddHours(-2), now, true);
        recent.Should().Be(0.50);
        favorite.Should().BeLessThan(recent);
    }

    [Fact]
    public void Mmr_rerank_limits_same_category_dominance()
    {
        var ranked = Enumerable.Range(0, 8).Select(i => new ScoredCandidate
        {
            Game = new CandidateGame
            {
                Id = Guid.NewGuid(),
                Title = $"G{i}",
                Category = i < 6 ? "Puzzle" : "Strategy",
                Tags = i < 6 ? ["puzzle"] : ["strategy"],
                DiscoveryScore = 90 - i
            },
            Score = new RecommendationScore { Final = 1 - (i * 0.01) }
        }).ToList();

        var selected = MmrReranker.Rerank(ranked, 5, 0.80);
        selected.Should().HaveCount(5);
        selected.Count(x => x.Game.Category == "Puzzle").Should().BeLessThan(5);
    }

    [Fact]
    public void Game_similarity_is_bounded()
    {
        var a = new CandidateGame { Category = "Puzzle", Tags = ["puzzle", "casual"], DiscoveryScore = 80 };
        var b = new CandidateGame { Category = "Puzzle", Tags = ["puzzle", "match3"], DiscoveryScore = 70 };
        var score = GameSimilarity.Calculate(a, b);
        score.Should().BeInRange(0, 100);
        score.Should().BeGreaterThan(40);
    }
}

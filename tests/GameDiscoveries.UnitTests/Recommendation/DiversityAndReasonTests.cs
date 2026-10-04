using FluentAssertions;
using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Domain;
using GameDiscoveries.Modules.Recommendation.Services;

namespace GameDiscoveries.UnitTests.Recommendation;

public sealed class DiversityAndReasonTests
{
    [Fact]
    public void Diversity_Limits_Same_Category_In_Top_10()
    {
        var ranked = Enumerable.Range(0, 20)
            .Select(i => new ScoredCandidate
            {
                Game = new CandidateGame
                {
                    Id = Guid.NewGuid(),
                    Title = $"Game {i}",
                    Category = i < 15 ? "Action" : "Puzzle",
                    Slug = $"game-{i}"
                },
                Score = new RecommendationScore { Final = 1 - (i * 0.01), Reason = "x" }
            })
            .ToList();

        var diversified = DiversityService.Diversify(ranked, 10, maxSameCategoryInTop10: 3, diversityWeight: 0.05);
        diversified.Count(x => x.Game.Category == "Action").Should().BeLessThanOrEqualTo(3);
        diversified.Should().Contain(x => x.Game.Category == "Puzzle");
    }

    [Fact]
    public void Reasons_Are_Deterministic()
    {
        var profile = new UserPreferenceProfile
        {
            PreferredCategories = new Dictionary<string, double> { ["Racing"] = 1 },
            FavoriteGameIds = [Guid.NewGuid()],
            Signals =
            [
                new UserSignal(Guid.NewGuid(), "favorite", DateTimeOffset.UtcNow, 1, 0, "Racing", [])
            ]
        };
        var game = new CandidateGame { Category = "Racing", Title = "Speed" };

        RecommendationReasonService.Build(RecommendationType.Trending, game, profile)
            .Should().Be("Trending now");
        RecommendationReasonService.Build(RecommendationType.HiddenGems, game, profile)
            .Should().Be("Hidden gem");
        RecommendationReasonService.Build(RecommendationType.ForYou, game, profile)
            .Should().Contain("Racing");
        RecommendationReasonService.Build(RecommendationType.SimilarGames, game, profile, "Neon Drift")
            .Should().Be("Similar to Neon Drift");
    }

    [Fact]
    public void Algorithm_Version_Constant()
    {
        new RecommendationOptions().AlgorithmVersion.Should().Be("v1");
    }
}

using FluentAssertions;
using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Domain;
using GameDiscoveries.Modules.Recommendation.Services;

namespace GameDiscoveries.UnitTests.Recommendation;

public sealed class RecommendationScorerTests
{
    [Fact]
    public void Content_Score_Prefers_Matching_Category_And_Tags()
    {
        var profile = new UserPreferenceProfile
        {
            PreferredCategories = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Action"] = 1.0
            },
            PreferredTags = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["cars"] = 1.0
            },
            MobilePreference = 0.8,
            MultiplayerPreference = 0.2
        };

        var game = new CandidateGame
        {
            Category = "Action",
            Tags = ["cars", "arcade"],
            MobileReady = true,
            Multiplayer = false
        };

        var score = RecommendationScorer.ComputeContentScore(game, profile, new ContentSimilarityWeights());
        score.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void Pair_Similarity_Is_High_For_Shared_Metadata()
    {
        var left = new CandidateGame
        {
            Category = "Racing",
            Tags = ["cars", "speed"],
            Orientation = "landscape",
            MobileReady = true,
            Multiplayer = false,
            Description = "Fast racing cars on track"
        };
        var right = new CandidateGame
        {
            Category = "Racing",
            Tags = ["cars", "drift"],
            Orientation = "landscape",
            MobileReady = true,
            Multiplayer = false,
            Description = "Racing cars drift track"
        };

        var score = RecommendationScorer.ComputePairSimilarity(left, right, new ContentSimilarityWeights());
        score.Should().BeGreaterThan(0.6);
    }

    [Fact]
    public void Final_Score_Is_Normalized()
    {
        var options = new RecommendationOptions();
        var profile = new UserPreferenceProfile
        {
            FavoriteGameIds = [Guid.NewGuid()],
            PlayedGameIds = [Guid.NewGuid()],
            PreferredCategories = new Dictionary<string, double> { ["Puzzle"] = 1 },
            Signals =
            [
                new UserSignal(Guid.NewGuid(), "favorite", DateTimeOffset.UtcNow, 1, 0, "Puzzle", ["match"])
            ]
        };

        var game = new CandidateGame
        {
            Category = "Puzzle",
            Tags = ["match"],
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-2),
            PopularityProxy = 4,
            EngagementProxy = 900
        };

        var score = RecommendationScorer.Score(
            game,
            profile,
            options,
            RecommendationType.ForYou,
            DateTimeOffset.UtcNow);

        score.Final.Should().BeInRange(0, 1);
        score.Content.Should().BeInRange(0, 1);
    }

    [Fact]
    public void Hidden_Gem_Penalizes_Popularity()
    {
        var options = new RecommendationOptions();
        var profile = new UserPreferenceProfile();
        var popular = new CandidateGame
        {
            Category = "Action",
            PopularityProxy = 20,
            EngagementProxy = 100,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var gem = new CandidateGame
        {
            Category = "Action",
            PopularityProxy = 1,
            EngagementProxy = 1200,
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };

        var now = DateTimeOffset.UtcNow;
        var popularScore = RecommendationScorer.Score(popular, profile, options, RecommendationType.HiddenGems, now);
        var gemScore = RecommendationScorer.Score(gem, profile, options, RecommendationType.HiddenGems, now);
        gemScore.Final.Should().BeGreaterThan(popularScore.Final);
    }
}

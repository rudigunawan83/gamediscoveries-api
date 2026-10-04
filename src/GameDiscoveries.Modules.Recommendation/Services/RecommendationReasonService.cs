using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.Modules.Recommendation.Services;

public static class RecommendationReasonService
{
    public static string Build(
        RecommendationType type,
        CandidateGame game,
        UserPreferenceProfile profile,
        string? seedTitle = null)
    {
        return type switch
        {
            RecommendationType.SimilarGames => string.IsNullOrWhiteSpace(seedTitle)
                ? $"Similar to {game.Category ?? "games"} you may like"
                : $"Similar to {seedTitle}",
            RecommendationType.BecauseYouPlayed => string.IsNullOrWhiteSpace(seedTitle)
                ? "Because you played similar games"
                : $"Because you played {seedTitle}",
            RecommendationType.Trending => "Trending now",
            RecommendationType.NewDiscoveries => "New discovery",
            RecommendationType.HiddenGems => "Hidden gem",
            RecommendationType.QuickPlay => "Quick play pick",
            RecommendationType.ForYou => BuildForYouReason(game, profile),
            _ => "Recommended for you"
        };
    }

    private static string BuildForYouReason(CandidateGame game, UserPreferenceProfile profile)
    {
        if (profile.IsColdStart)
        {
            return game.SourceBucket switch
            {
                "trending" => "Trending now",
                "popular" => "Popular right now",
                "new" => "New discovery",
                _ => "Popular with players like you"
            };
        }

        if (!string.IsNullOrWhiteSpace(game.Category)
            && profile.PreferredCategories.TryGetValue(game.Category, out var catScore)
            && catScore >= 0.4)
        {
            var favorited = profile.FavoriteGameIds.Count > 0
                            && profile.Signals.Any(s =>
                                s.Kind == "favorite"
                                && string.Equals(s.Category, game.Category, StringComparison.OrdinalIgnoreCase));
            return favorited
                ? $"Because you favorited {game.Category} games"
                : $"Because you played {game.Category} games";
        }

        if (game.Tags.Any(t => profile.PreferredTags.ContainsKey(t)))
        {
            return "Similar to games you played";
        }

        return "Recommended for you";
    }
}

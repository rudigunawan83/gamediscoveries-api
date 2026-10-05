using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.Modules.Recommendation.Services;

public static class RecommendationReasonService
{
    public static RecommendationReasonDto BuildDetail(
        RecommendationType type,
        CandidateGame game,
        UserPreferenceProfile profile,
        string? seedTitle = null)
    {
        var (reasonType, label) = type switch
        {
            RecommendationType.SimilarGames => (
                "SIMILAR_GAME",
                string.IsNullOrWhiteSpace(seedTitle)
                    ? $"Similar to {game.Category ?? "games"} you may like"
                    : $"Similar to {seedTitle}"),
            RecommendationType.BecauseYouPlayed => (
                "BECAUSE_YOU_PLAYED",
                string.IsNullOrWhiteSpace(seedTitle)
                    ? "Because you played similar games"
                    : $"Because you played {seedTitle}"),
            RecommendationType.Trending => ("TRENDING", "Trending for you"),
            RecommendationType.NewDiscoveries => ("NEW_FOR_YOU", "New game you might like"),
            RecommendationType.HiddenGems => ("EXPLORATION", "Explore something different"),
            RecommendationType.QuickPlay => ("QUICK_PLAY", "Quick play pick"),
            RecommendationType.ForYou => BuildForYou(game, profile),
            _ => ("RECOMMENDED", "Recommended for you")
        };

        return new RecommendationReasonDto(reasonType, label);
    }

    public static string Build(
        RecommendationType type,
        CandidateGame game,
        UserPreferenceProfile profile,
        string? seedTitle = null) =>
        BuildDetail(type, game, profile, seedTitle).Label;

    private static (string Type, string Label) BuildForYou(CandidateGame game, UserPreferenceProfile profile)
    {
        if (profile.IsColdStart)
        {
            return game.SourceBucket switch
            {
                "trending" => ("TRENDING", "Trending now"),
                "popular" => ("POPULAR", "Popular right now"),
                "new" => ("NEW_FOR_YOU", "New discovery"),
                _ => ("COLD_START", "Popular with players like you")
            };
        }

        if (game.TrendingScore >= 70)
        {
            return ("TRENDING_FOR_YOU", "Trending in games you like");
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
                ? ("FAVORITE_SIMILAR", $"Because you favorited {game.Category} games")
                : ("GENRE_MATCH", $"Because you like {game.Category} games");
        }

        if (game.Tags.Any(t => profile.PreferredTags.ContainsKey(t)))
        {
            return ("TAG_MATCH", "Similar to games you played");
        }

        return ("PERSONALIZED", "Recommended for you");
    }
}

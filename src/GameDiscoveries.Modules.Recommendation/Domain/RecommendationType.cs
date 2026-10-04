namespace GameDiscoveries.Modules.Recommendation.Domain;

public enum RecommendationType
{
    ForYou = 0,
    SimilarGames = 1,
    BecauseYouPlayed = 2,
    Trending = 3,
    NewDiscoveries = 4,
    HiddenGems = 5,
    QuickPlay = 6
}

public static class RecommendationTypeExtensions
{
    public static string ToApiValue(this RecommendationType type) => type switch
    {
        RecommendationType.ForYou => "for-you",
        RecommendationType.SimilarGames => "similar-games",
        RecommendationType.BecauseYouPlayed => "because-you-played",
        RecommendationType.Trending => "trending",
        RecommendationType.NewDiscoveries => "new-discoveries",
        RecommendationType.HiddenGems => "hidden-gems",
        RecommendationType.QuickPlay => "quick-play",
        _ => "for-you"
    };
}

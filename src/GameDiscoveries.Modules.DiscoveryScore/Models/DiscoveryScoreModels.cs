namespace GameDiscoveries.Modules.DiscoveryScore.Models;

public sealed record DiscoveryGameCardDto(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    string? ThumbnailUrl,
    string? CoverUrl,
    string? Category,
    double? AverageRating,
    DateTimeOffset? PublishedAt);

public sealed record DiscoveryRankingItemDto(
    int Rank,
    double Score,
    string Trend,
    double TrendPercentage,
    int? PreviousRank,
    int RankChange,
    DiscoveryGameCardDto Game);

public sealed record DiscoveryRankingResponse(
    string Type,
    string Period,
    IReadOnlyList<DiscoveryRankingItemDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DiscoveryScoreFactorDto(string Name, double Score, string Impact);

public sealed record DiscoveryScoreExplainDto(
    Guid GameId,
    double Score,
    double TrendingScore,
    string Trend,
    double TrendPercentage,
    int ScoreVersion,
    DateTimeOffset CalculatedAt,
    IReadOnlyList<DiscoveryScoreFactorDto> Factors);

public sealed record AdminDiscoveryOverviewDto(
    long ScoredGames,
    double AverageDiscoveryScore,
    double AverageTrendingScore,
    long RisingGames,
    long HotGames,
    long DecliningGames,
    long NewGames,
    DateTimeOffset? LastCalculatedAt,
    int ScoreVersion);

public sealed record AdminDiscoveryConfigDto(
    int ScoreVersion,
    double PopularityWeight,
    double EngagementWeight,
    double QualityWeight,
    double MomentumWeight,
    double GrowthWeight,
    double FreshnessWeight,
    double TrendingRecentWeight,
    double TrendingMomentumWeight,
    double TrendingGrowthWeight,
    double TrendingEngagementWeight,
    double TrendingFreshnessWeight,
    double FreshnessDecayDays,
    int NewGameDays,
    int MinValidSessionsForNewTrending,
    double GrowthSmoothing,
    double BayesianM,
    double RisingGrowthThreshold,
    double DecliningGrowthThreshold,
    int ScoreValidMinutes,
    DateTimeOffset UpdatedAt);

public sealed record UpsertDiscoveryConfigRequest(
    double PopularityWeight,
    double EngagementWeight,
    double QualityWeight,
    double MomentumWeight,
    double GrowthWeight,
    double FreshnessWeight,
    double TrendingRecentWeight,
    double TrendingMomentumWeight,
    double TrendingGrowthWeight,
    double TrendingEngagementWeight,
    double TrendingFreshnessWeight,
    double FreshnessDecayDays,
    int NewGameDays,
    int MinValidSessionsForNewTrending,
    double GrowthSmoothing,
    double BayesianM,
    double RisingGrowthThreshold,
    double DecliningGrowthThreshold,
    int ScoreValidMinutes,
    string? Reason);

public sealed class DiscoveryConfigEntity
{
    public Guid Id { get; set; }
    public int ScoreVersion { get; set; }
    public double PopularityWeight { get; set; }
    public double EngagementWeight { get; set; }
    public double QualityWeight { get; set; }
    public double MomentumWeight { get; set; }
    public double GrowthWeight { get; set; }
    public double FreshnessWeight { get; set; }
    public double TrendingRecentWeight { get; set; }
    public double TrendingMomentumWeight { get; set; }
    public double TrendingGrowthWeight { get; set; }
    public double TrendingEngagementWeight { get; set; }
    public double TrendingFreshnessWeight { get; set; }
    public double FreshnessDecayDays { get; set; }
    public int NewGameDays { get; set; }
    public int MinValidSessionsForNewTrending { get; set; }
    public double GrowthSmoothing { get; set; }
    public double BayesianM { get; set; }
    public double RisingGrowthThreshold { get; set; }
    public double DecliningGrowthThreshold { get; set; }
    public int ScoreValidMinutes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public sealed class GameSignalRow
{
    public Guid GameId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int Views24h { get; set; }
    public int ViewsPrev24h { get; set; }
    public int Starts24h { get; set; }
    public int StartsPrev24h { get; set; }
    public int Sessions24h { get; set; }
    public int ValidSessions24h { get; set; }
    public int ValidSessionsPrev24h { get; set; }
    public long ActiveSeconds24h { get; set; }
    public int UniqueUsers24h { get; set; }
    public int UniqueUsersPrev24h { get; set; }
    public int Favorites24h { get; set; }
    public int FavoritesPrev24h { get; set; }
    public int FavoritesLifetime { get; set; }
    public int RatingsLifetime { get; set; }
    public int ReviewsLifetime { get; set; }
    public double AvgRating { get; set; }
    public int ReturningUsers24h { get; set; }
    public int ValidSessionsLifetime { get; set; }
    public long ActiveSecondsLifetime { get; set; }
}

public sealed class DiscoveryScoreEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public double Score { get; set; }
    public double PopularityScore { get; set; }
    public double EngagementScore { get; set; }
    public double QualityScore { get; set; }
    public double MomentumScore { get; set; }
    public double GrowthScore { get; set; }
    public double FreshnessScore { get; set; }
    public double TrendingScore { get; set; }
    public string TrendState { get; set; } = "STABLE";
    public double TrendPercentage { get; set; }
    public int ScoreVersion { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
}

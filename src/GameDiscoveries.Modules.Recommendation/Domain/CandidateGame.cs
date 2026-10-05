namespace GameDiscoveries.Modules.Recommendation.Domain;

public sealed class CandidateGame
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? Category { get; init; }
    public string? Orientation { get; init; }
    public string? Platform { get; init; }
    public bool MobileReady { get; init; }
    public bool Multiplayer { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public double PopularityProxy { get; init; }
    public double EngagementProxy { get; init; }
    public int GlobalPlaySessions { get; init; }
    public double DiscoveryScore { get; init; }
    public double TrendingScore { get; init; }
    public double FreshnessScore { get; init; }
    public double MomentumScore { get; init; }
    public string SourceBucket { get; init; } = "catalog";
}

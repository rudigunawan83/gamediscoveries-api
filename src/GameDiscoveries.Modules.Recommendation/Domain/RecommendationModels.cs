namespace GameDiscoveries.Modules.Recommendation.Domain;

public sealed record RecommendationGameDto(
    Guid Id,
    string Slug,
    string Title,
    string? ThumbnailUrl,
    string? Category,
    string? Orientation);

public sealed record RecommendationItemDto(
    RecommendationGameDto Game,
    double Score,
    int Rank,
    string Reason);

public sealed record RecommendationResponse(
    IReadOnlyList<RecommendationItemDto> Items,
    string Type,
    string AlgorithmVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt,
    bool CacheHit);

public sealed record RecommendationDebugItem(
    Guid GameId,
    string Title,
    string? Category,
    RecommendationScore Score,
    int Rank,
    string Reason);

public sealed record RecommendationDebugResponse(
    string Type,
    string AlgorithmVersion,
    int CandidateCount,
    bool CacheHit,
    bool ColdStart,
    IReadOnlyList<RecommendationDebugItem> Items);

namespace GameDiscoveries.Modules.Recommendation.Domain;

public sealed record RecommendationGameDto(
    Guid Id,
    string Slug,
    string Title,
    string? ThumbnailUrl,
    string? Category,
    string? Orientation);

public sealed record RecommendationReasonDto(string Type, string Label);

public sealed record RecommendationItemDto(
    RecommendationGameDto Game,
    double Score,
    int Rank,
    string Reason,
    RecommendationReasonDto? ReasonDetail = null,
    int Position = 0);

public sealed record RecommendationResponse(
    IReadOnlyList<RecommendationItemDto> Items,
    string Type,
    string AlgorithmVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt,
    bool CacheHit,
    string Strategy = "PERSONALIZED",
    int ProfileLevel = 0,
    Guid? RecommendationRequestId = null);

public sealed record RecommendationHomeSectionDto(
    string Type,
    string Title,
    IReadOnlyList<RecommendationItemDto> Items);

public sealed record RecommendationHomeResponse(
    IReadOnlyList<RecommendationHomeSectionDto> Sections,
    string AlgorithmVersion,
    int ProfileLevel,
    Guid RecommendationRequestId);

public sealed record RecommendationFeedbackRequest(
    string FeedbackType,
    string? Section = null,
    Guid? RecommendationRequestId = null,
    Guid? AnonymousId = null);

public sealed record RecommendationImpressionRequest(
    Guid RecommendationRequestId,
    Guid GameId,
    int Position,
    string Section,
    string EventType = "IMPRESSION",
    Guid? AnonymousId = null);

public sealed record AdminRecommendationOverviewDto(
    long Requests,
    long Impressions,
    long Clicks,
    double Ctr,
    long FeedbackCount,
    double PersonalizedShare,
    DateTimeOffset? LastRequestAt);

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

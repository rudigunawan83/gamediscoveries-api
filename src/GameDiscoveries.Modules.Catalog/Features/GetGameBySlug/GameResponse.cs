namespace GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

public sealed record GameResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    string? ThumbnailUrl,
    string? CoverUrl,
    string? GameUrl,
    string Status,
    bool MobileReady,
    string? Orientation,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt);

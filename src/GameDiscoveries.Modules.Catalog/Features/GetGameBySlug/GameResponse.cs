namespace GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

public sealed record GameResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    string? ThumbnailUrl,
    string? CoverUrl,
    string? GameUrl,
    string? EmbedUrl,
    string? Category,
    string? Developer,
    string? Platform,
    string Status,
    bool MobileReady,
    string? Orientation,
    int? Width,
    int? Height,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt);

namespace GameDiscoveries.Modules.Catalog.Features.ListGames;

public sealed record GameSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    string? ThumbnailUrl,
    string? CoverUrl,
    string? GameUrl,
    string? Category,
    string? Platform,
    bool MobileReady,
    DateTimeOffset? PublishedAt);

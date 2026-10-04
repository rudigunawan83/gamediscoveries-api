namespace GameDiscoveries.Modules.Catalog.Features.ListGames;

public sealed class GameSummaryRow
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? CoverUrl { get; init; }
    public string? GameUrl { get; init; }
    public string? Category { get; init; }
    public string? Platform { get; init; }
    public bool MobileReady { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }

    public GameSummaryResponse ToResponse() => new(
        Id,
        Slug,
        Title,
        Description,
        ThumbnailUrl,
        CoverUrl,
        GameUrl,
        Category,
        Platform,
        MobileReady,
        PublishedAt);
}

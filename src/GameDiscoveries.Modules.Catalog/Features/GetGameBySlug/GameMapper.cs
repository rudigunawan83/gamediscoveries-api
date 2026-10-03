namespace GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

public static class GameMapper
{
    public static GameResponse ToResponse(GameRow row) => new(
        row.Id,
        row.Slug,
        row.Title,
        row.Description,
        row.ThumbnailUrl,
        row.CoverUrl,
        row.GameUrl,
        row.Status,
        row.MobileReady,
        row.Orientation,
        row.CreatedAt,
        row.UpdatedAt,
        row.PublishedAt);
}

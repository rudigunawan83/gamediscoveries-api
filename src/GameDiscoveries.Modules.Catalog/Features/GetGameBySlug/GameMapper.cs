using GameDiscoveries.BuildingBlocks.Text;

namespace GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

public static class GameMapper
{
    public static GameResponse ToResponse(GameRow row, IReadOnlyList<string> tags) => new(
        row.Id,
        row.Slug,
        row.Title,
        HtmlText.Decode(row.Description),
        HtmlText.Decode(row.Instructions),
        row.ThumbnailUrl,
        row.CoverUrl,
        row.GameUrl,
        row.EmbedUrl,
        row.Category,
        row.Developer,
        row.Platform,
        row.Status,
        row.MobileReady,
        row.Orientation,
        row.Width,
        row.Height,
        tags,
        row.CreatedAt,
        row.UpdatedAt,
        row.PublishedAt);
}

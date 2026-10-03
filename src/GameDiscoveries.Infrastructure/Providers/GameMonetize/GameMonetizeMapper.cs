using System.Text.Json;
using GameDiscoveries.Infrastructure.Providers.Abstractions;
using GameDiscoveries.Infrastructure.Providers.GameMonetize.Models;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public static class GameMonetizeMapper
{
    public static ExternalGame? Map(GameMonetizeFeedItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title))
        {
            return null;
        }

        var tags = string.IsNullOrWhiteSpace(item.Tags)
            ? Array.Empty<string>()
            : item.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var categories = string.IsNullOrWhiteSpace(item.Category)
            ? Array.Empty<string>()
            : [item.Category];

        return new ExternalGame
        {
            ProviderGameId = item.Id,
            Title = item.Title,
            Description = item.Description,
            ThumbnailUrl = item.Thumb,
            CoverUrl = item.Thumb,
            GameUrl = item.Url,
            ProviderUrl = item.Url,
            MobileReady = true,
            Orientation = InferOrientation(item.Width, item.Height),
            Categories = categories,
            Tags = tags,
            RawPayload = JsonSerializer.Serialize(item)
        };
    }

    private static string InferOrientation(string? width, string? height)
    {
        if (!int.TryParse(width, out var w) || !int.TryParse(height, out var h) || w <= 0 || h <= 0)
        {
            return "landscape";
        }

        return h > w ? "portrait" : "landscape";
    }
}

using System.Text.Json;
using GameDiscoveries.BuildingBlocks.Text;
using GameDiscoveries.Infrastructure.Providers.Abstractions;
using GameDiscoveries.Infrastructure.Providers.GameMonetize.Models;
using GameDiscoveries.Infrastructure.Providers.Normalization;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public static class GameMonetizeMapper
{
    public const string SourceName = "GameMonetize";

    public static ExternalGame? Map(GameMonetizeFeedItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Title))
        {
            return null;
        }

        var providerGameId = ResolveProviderGameId(item);
        if (string.IsNullOrWhiteSpace(providerGameId))
        {
            return null;
        }

        var tags = TagNormalizer.Normalize(
            string.IsNullOrWhiteSpace(item.Tags)
                ? Array.Empty<string>()
                : item.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        var categories = ProviderCategoryMapper.MapGameMonetizeMany(
            string.IsNullOrWhiteSpace(item.Category)
                ? Array.Empty<string>()
                : [item.Category.Trim()]);

        var mobileReady = InferMobileReady(item, tags, categories);
        int? width = int.TryParse(item.Width, out var w) && w > 0 ? w : null;
        int? height = int.TryParse(item.Height, out var h) && h > 0 ? h : null;

        return new ExternalGame
        {
            ProviderGameId = providerGameId,
            Title = item.Title.Trim(),
            Description = BuildingBlocks.Text.HtmlText.Decode(item.Description),
            Instructions = BuildingBlocks.Text.HtmlText.Decode(item.Instructions),
            ThumbnailUrl = item.Thumb,
            CoverUrl = item.Thumb,
            GameUrl = item.Url,
            EmbedUrl = item.Url,
            ProviderUrl = item.Url,
            Developer = string.IsNullOrWhiteSpace(item.Company) ? null : item.Company.Trim(),
            MobileReady = mobileReady,
            Platform = mobileReady ? "mobile" : "web",
            Orientation = InferOrientation(width, height),
            Width = width,
            Height = height,
            Categories = categories,
            Tags = tags,
            RawPayload = JsonSerializer.Serialize(item)
        };
    }

    public static string ResolveProviderGameId(GameMonetizeFeedItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            return item.Id.Trim();
        }

        if (!string.IsNullOrWhiteSpace(item.Url))
        {
            return "url:" + SlugGenerator.DeterministicExternalId(SourceName, item.Url);
        }

        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            return "title:" + SlugGenerator.DeterministicExternalId(SourceName, item.Title);
        }

        return string.Empty;
    }

    private static bool InferMobileReady(
        GameMonetizeFeedItem item,
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<string> categories)
    {
        static bool HasToken(IEnumerable<string> values, string token) =>
            values.Any(v => v.Contains(token, StringComparison.OrdinalIgnoreCase));

        if (HasToken(tags, "mobile") || HasToken(categories, "mobile"))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item.Category)
            && item.Category.Contains("mobile", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Default true for browser HTML5 feeds; catalog can refine later.
        return true;
    }

    private static string InferOrientation(int? width, int? height)
    {
        if (width is null or <= 0 || height is null or <= 0)
        {
            return "landscape";
        }

        if (height > width)
        {
            return "portrait";
        }

        if (width > height)
        {
            return "landscape";
        }

        return "both";
    }
}

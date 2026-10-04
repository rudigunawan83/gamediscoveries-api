using System.Net;
using System.Text.RegularExpressions;

namespace GameDiscoveries.Modules.Community.Services;

public static partial class ContentSanitizer
{
    public static string SanitizePlainText(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(input);
        var noTags = HtmlTagRegex().Replace(decoded, string.Empty);
        var normalized = WhitespaceRegex().Replace(noTags, " ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].Trim();
    }

    public static string ToSlug(string title)
    {
        var slug = SanitizePlainText(title, 180).ToLowerInvariant();
        slug = NonSlugRegex().Replace(slug, "-");
        slug = CollapseDashRegex().Replace(slug, "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "post";
        }

        return slug.Length <= 180 ? slug : slug[..180].Trim('-');
    }

    public static string ToUsername(string? source)
    {
        var value = SanitizePlainText(source, 50).ToLowerInvariant();
        value = NonSlugRegex().Replace(value, "-");
        value = CollapseDashRegex().Replace(value, "-").Trim('-');
        return string.IsNullOrWhiteSpace(value) ? "player" : value[..Math.Min(value.Length, 40)];
    }

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9\-]+")]
    private static partial Regex NonSlugRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex CollapseDashRegex();
}

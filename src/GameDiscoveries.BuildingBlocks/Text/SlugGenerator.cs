using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GameDiscoveries.BuildingBlocks.Text;

public static partial class SlugGenerator
{
    public static string FromTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var normalized = title.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : '-');
        }

        var slug = CollapseDashes().Replace(builder.ToString(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "game" : slug;
    }

    public static string EnsureUnique(string baseSlug, Guid entityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseSlug);
        var suffix = entityId.ToString("N")[..8];
        return $"{baseSlug}-{suffix}";
    }

    public static string DeterministicExternalId(string source, string urlOrKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlOrKey);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{source}|{urlOrKey.Trim()}"));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseDashes();
}

namespace GameDiscoveries.Infrastructure.Providers.Normalization;

public static class TagNormalizer
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var cleaned = raw
                .Trim()
                .Replace('_', ' ')
                .Replace('/', ' ')
                .Replace('|', ' ');

            while (cleaned.Contains("  ", StringComparison.Ordinal))
            {
                cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
            }

            cleaned = cleaned.Trim();
            if (cleaned.Length == 0 || !seen.Add(cleaned))
            {
                continue;
            }

            result.Add(cleaned);
        }

        return result;
    }

    public static string NormalizeKey(string tag) =>
        tag.Trim().ToLowerInvariant();
}

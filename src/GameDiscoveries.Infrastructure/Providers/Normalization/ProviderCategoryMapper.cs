namespace GameDiscoveries.Infrastructure.Providers.Normalization;

/// <summary>
/// Maps provider category labels to GameDiscoveries category names.
/// Keep mappings out of controllers/endpoints.
/// </summary>
public static class ProviderCategoryMapper
{
    private static readonly Dictionary<string, string> GameMonetizeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["2 Player"] = "2 Player",
        ["2-Player"] = "2 Player",
        ["Two Player"] = "2 Player",
        ["Action"] = "Action",
        ["Adventure"] = "Adventure",
        ["Arcade"] = "Arcade",
        ["Boys"] = "Boys",
        ["Clicker"] = "Hypercasual",
        ["Girls"] = "Girls",
        ["Hypercasual"] = "Hypercasual",
        ["Multiplayer"] = "Multiplayer",
        ["Puzzle"] = "Puzzle",
        ["Puzzles"] = "Puzzle",
        ["Racing"] = "Racing",
        ["Shooting"] = "Shooting",
        ["Sports"] = "Sports",
        ["3D"] = "3D",
        [".IO"] = "Multiplayer",
        ["IO"] = "Multiplayer",
    };

    public static string MapGameMonetize(string? providerCategory)
    {
        if (string.IsNullOrWhiteSpace(providerCategory))
        {
            return "Uncategorized";
        }

        var trimmed = providerCategory.Trim();
        return GameMonetizeMap.TryGetValue(trimmed, out var mapped)
            ? mapped
            : trimmed;
    }

    public static IReadOnlyList<string> MapGameMonetizeMany(IEnumerable<string>? categories)
    {
        if (categories is null)
        {
            return [];
        }

        return categories
            .Select(MapGameMonetize)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

namespace GameDiscoveries.Modules.Users.Models;

/// <summary>Allowed values of <c>users.preferred_language</c>, kept in sync with its CHECK constraint.</summary>
public static class PreferredLanguage
{
    public const string System = "SYSTEM";
    public const string English = "en";
    public const string Indonesian = "id";

    public static IReadOnlyList<string> All { get; } = [System, English, Indonesian];

    /// <summary>Matches case-insensitively and returns the canonical stored value.</summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        var trimmed = value?.Trim();
        normalized = All.FirstOrDefault(v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalized.Length > 0;
    }
}

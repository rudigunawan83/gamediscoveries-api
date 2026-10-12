namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public sealed class SearchConsoleOptions
{
    public const string SectionName = "SearchConsole";

    /// <summary>Search Console property, exactly as registered (URL-prefix properties end with a slash).</summary>
    public string SiteUrl { get; set; } = "https://www.gamediscoveries.com/";

    /// <summary>Path to a Google service account JSON key with read access to <see cref="SiteUrl"/>.</summary>
    public string? ServiceAccountKeyPath { get; set; }

    /// <summary>The same JSON key, base64-encoded, for hosts that only offer environment variables.</summary>
    public string? ServiceAccountJsonBase64 { get; set; }

    /// <summary>Chrome UX Report API key; Core Web Vitals stay unavailable without it.</summary>
    public string? CruxApiKey { get; set; }

    public string CruxOrigin { get; set; } = "https://www.gamediscoveries.com";
}

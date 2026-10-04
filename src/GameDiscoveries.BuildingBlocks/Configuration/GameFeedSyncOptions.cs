namespace GameDiscoveries.BuildingBlocks.Configuration;

public sealed class GameFeedSyncOptions
{
    public const string SectionName = "GameFeedSync";

    public bool Enabled { get; set; }

    /// <summary>
    /// Shared secret for manual sync endpoints when JWT auth is disabled.
    /// Send via header: X-GameDiscoveries-Admin-Key
    /// </summary>
    public string? AdminApiKey { get; set; }

    public int LatestIntervalMinutes { get; set; } = 60;

    public int PopularIntervalMinutes { get; set; } = 120;

    public int CategoryIntervalMinutes { get; set; } = 360;

    public int MobileIntervalMinutes { get; set; } = 360;

    public int TwoPlayerIntervalMinutes { get; set; } = 360;

    public int FeaturedIntervalMinutes { get; set; } = 360;

    public int StartupDelaySeconds { get; set; } = 20;
}

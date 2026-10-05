namespace GameDiscoveries.Modules.Analytics.Options;

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public bool Enabled { get; set; } = true;

    public int MaxBatchSize { get; set; } = 100;

    public int MaxMetadataSizeKb { get; set; } = 16;

    public bool EnableAnonymousTracking { get; set; } = true;

    public bool ValidateGameExists { get; set; } = true;

    /// <summary>
    /// Salt used when hashing client IP for privacy-preserving storage.
    /// </summary>
    public string IpHashSalt { get; set; } = "gamediscoveries-analytics";
}

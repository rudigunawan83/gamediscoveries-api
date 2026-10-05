namespace GameDiscoveries.Modules.Streaks.Options;

public sealed class StreakOptions
{
    public const string SectionName = "Streak";

    public bool Enabled { get; set; } = true;

    /// <summary>IANA timezone for streak calendar days. Default Asia/Jakarta.</summary>
    public string TimeZone { get; set; } = "Asia/Jakarta";

    public int MinimumActiveSeconds { get; set; } = 30;

    public FreezeOptions Freeze { get; set; } = new();

    public sealed class FreezeOptions
    {
        public bool Enabled { get; set; } = true;
        public int MaxStored { get; set; } = 2;
        public int DefaultCount { get; set; } = 0;
    }
}

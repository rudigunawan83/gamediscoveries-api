namespace GameDiscoveries.Modules.Leaderboards.Options;

public sealed class LeaderboardOptions
{
    public const string SectionName = "Leaderboard";

    public bool Enabled { get; set; } = true;
    public string Version { get; set; } = "LEADERBOARD_V1";
    public string Timezone { get; set; } = "Asia/Jakarta";
    public bool IncludeAdminAdjustmentInLeaderboard { get; set; } = false;
    public int TopMaxLimit { get; set; } = 100;
    public int DefaultLimit { get; set; } = 50;
    public int TopCacheSeconds { get; set; } = 60;
    public int UserRankCacheSeconds { get; set; } = 45;
    public int ReconciliationIntervalMinutes { get; set; } = 60;
    public int SnapshotIntervalMinutes { get; set; } = 10;
    public int PeriodRotationIntervalMinutes { get; set; } = 5;
    public bool CreditPrimaryGenreOnly { get; set; } = true;
}

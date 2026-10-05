namespace GameDiscoveries.Modules.DiscoveryScore.Options;

public sealed class DiscoveryScoreOptions
{
    public const string SectionName = "DiscoveryScore";

    public bool Enabled { get; set; } = true;

    public int ScoreVersion { get; set; } = 1;

    public double PopularityWeight { get; set; } = 0.20;
    public double EngagementWeight { get; set; } = 0.25;
    public double QualityWeight { get; set; } = 0.15;
    public double MomentumWeight { get; set; } = 0.20;
    public double GrowthWeight { get; set; } = 0.10;
    public double FreshnessWeight { get; set; } = 0.10;

    public double TrendingRecentWeight { get; set; } = 0.30;
    public double TrendingMomentumWeight { get; set; } = 0.30;
    public double TrendingGrowthWeight { get; set; } = 0.20;
    public double TrendingEngagementWeight { get; set; } = 0.15;
    public double TrendingFreshnessWeight { get; set; } = 0.05;

    public double FreshnessDecayDays { get; set; } = 30;
    public int NewGameDays { get; set; } = 14;
    public int MinValidSessionsForNewTrending { get; set; } = 3;
    public double GrowthSmoothing { get; set; } = 10;
    public double BayesianM { get; set; } = 20;
    public double RisingGrowthThreshold { get; set; } = 25;
    public double DecliningGrowthThreshold { get; set; } = -20;
    public int ScoreValidMinutes { get; set; } = 90;
    public int CacheTtlMinutes { get; set; } = 10;
    public int AggregationIntervalMinutes { get; set; } = 60;
    public int RetentionHourlyDays { get; set; } = 90;
    public int RetentionDailyDays { get; set; } = 365;
    public int RankingLimit { get; set; } = 100;
}

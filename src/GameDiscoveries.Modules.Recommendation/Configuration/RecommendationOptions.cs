namespace GameDiscoveries.Modules.Recommendation.Configuration;

public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendation";

    public string AlgorithmVersion { get; set; } = "PERSONALIZED_V1";
    public int DefaultLimit { get; set; } = 20;
    public int CandidateLimit { get; set; } = 200;
    public int MaxSameCategoryInTop10 { get; set; } = 4;
    public double ExplorationRatio { get; set; } = 0.10;
    public double MmrLambda { get; set; } = 0.80;
    public int ColdStartThreshold { get; set; } = 3;

    public RecommendationWeights Weights { get; set; } = new();
    public ContentSimilarityWeights ContentWeights { get; set; } = new();
    public ColdStartMix ColdStart { get; set; } = new();
    public TimeDecayOptions TimeDecay { get; set; } = new();
    public CacheTtlOptions CacheTtl { get; set; } = new();
    public ExclusionOptions Exclusion { get; set; } = new();
}

public sealed class RecommendationWeights
{
    public double Content { get; set; } = 0.25;
    public double Preference { get; set; } = 0.20;
    public double Behavior { get; set; } = 0.20;
    public double Popularity { get; set; } = 0.10;
    public double Freshness { get; set; } = 0.10;
    public double Engagement { get; set; } = 0.05;
    public double Exploration { get; set; } = 0.05;
    public double Diversity { get; set; } = 0.05;
}

public sealed class ContentSimilarityWeights
{
    public double Category { get; set; } = 0.35;
    public double Tag { get; set; } = 0.35;
    public double GameType { get; set; } = 0.10;
    public double Orientation { get; set; } = 0.05;
    public double Mobile { get; set; } = 0.05;
    public double Multiplayer { get; set; } = 0.05;
    public double Description { get; set; } = 0.05;
}

public sealed class ColdStartMix
{
    public double Trending { get; set; } = 0.40;
    public double Popular { get; set; } = 0.30;
    public double NewDiscoveries { get; set; } = 0.20;
    public double Exploration { get; set; } = 0.10;
}

public sealed class TimeDecayOptions
{
    public double Day0 { get; set; } = 1.00;
    public double Day3 { get; set; } = 0.85;
    public double Day7 { get; set; } = 0.65;
    public double Day14 { get; set; } = 0.45;
    public double Day30 { get; set; } = 0.25;
    public double Day60Plus { get; set; } = 0.10;
}

public sealed class CacheTtlOptions
{
    public int ForYouSeconds { get; set; } = 600;
    public int BecauseYouPlayedSeconds { get; set; } = 600;
    public int SimilarSeconds { get; set; } = 1800;
    public int TrendingSeconds { get; set; } = 420;
    public int NewDiscoveriesSeconds { get; set; } = 1200;
    public int HiddenGemsSeconds { get; set; } = 900;
}

public sealed class ExclusionOptions
{
    public bool ExcludePlayedForYou { get; set; } = true;
    public bool ExcludeFavoritedForYou { get; set; } = false;
    public bool ExcludePlayedBecauseYouPlayed { get; set; } = true;
    public bool ExcludeFavoritedBecauseYouPlayed { get; set; } = false;
}

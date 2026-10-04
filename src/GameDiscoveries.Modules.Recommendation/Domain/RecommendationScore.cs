namespace GameDiscoveries.Modules.Recommendation.Domain;

public sealed class RecommendationScore
{
    public double Content { get; init; }
    public double Preference { get; init; }
    public double Behavior { get; init; }
    public double Popularity { get; init; }
    public double Freshness { get; init; }
    public double Engagement { get; init; }
    public double Exploration { get; init; }
    public double Diversity { get; init; }
    public double Final { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class ScoredCandidate
{
    public required CandidateGame Game { get; init; }
    public required RecommendationScore Score { get; init; }
}

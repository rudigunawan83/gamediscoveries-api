using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.Modules.Recommendation.Services;

/// <summary>
/// Abstraction for candidate scoring. Current: rule-based. Future: ML without API changes.
/// </summary>
public interface IRecommendationModel
{
    string Version { get; }

    Task<IReadOnlyList<ScoredCandidate>> ScoreCandidatesAsync(
        RecommendationContext context,
        IReadOnlyList<CandidateGame> candidates,
        CancellationToken cancellationToken = default);
}

public sealed record RecommendationContext(
    RecommendationType Type,
    UserPreferenceProfile Profile,
    RecommendationOptions Options,
    DateTimeOffset Now,
    CandidateGame? Seed = null);

/// <summary>
/// Deterministic PERSONALIZED_V1 scoring model (no ML).
/// </summary>
public sealed class RuleBasedRecommendationModel : IRecommendationModel
{
    public string Version => "PERSONALIZED_V1";

    public Task<IReadOnlyList<ScoredCandidate>> ScoreCandidatesAsync(
        RecommendationContext context,
        IReadOnlyList<CandidateGame> candidates,
        CancellationToken cancellationToken = default)
    {
        var scored = candidates
            .Select(game => new ScoredCandidate
            {
                Game = game,
                Score = RecommendationScorer.Score(
                    game,
                    context.Profile,
                    context.Options,
                    context.Type,
                    context.Now,
                    context.Seed)
            })
            .Where(x => x.Score.Final > 0)
            .OrderByDescending(x => x.Score.Final)
            .ThenByDescending(x => x.Game.DiscoveryScore)
            .ThenByDescending(x => x.Game.TrendingScore)
            .ThenByDescending(x => x.Game.FreshnessScore)
            .ThenBy(x => x.Game.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<ScoredCandidate>>(scored);
    }
}

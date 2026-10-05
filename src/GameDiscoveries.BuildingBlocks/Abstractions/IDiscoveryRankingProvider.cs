namespace GameDiscoveries.BuildingBlocks.Abstractions;

/// <summary>
/// Optional provider of precomputed discovery rankings for home feed / catalog.
/// Implemented by Modules.DiscoveryScore.
/// </summary>
public interface IDiscoveryRankingProvider
{
    Task<IReadOnlyList<Guid>> GetTrendingGameIdsAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

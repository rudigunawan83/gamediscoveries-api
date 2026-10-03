using GameDiscoveries.BuildingBlocks.Abstractions;

namespace GameDiscoveries.Infrastructure.Meilisearch;

public sealed class NoOpSearchService : ISearchService
{
    public Task IndexAsync<T>(
        string index,
        IEnumerable<T> documents,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteAsync(
        string index,
        string id,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<DocumentSearchResult<T>> SearchAsync<T>(
        string index,
        string query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new DocumentSearchResult<T>
        {
            Hits = [],
            EstimatedTotalHits = 0,
            Query = query,
            ProcessingTimeMs = 0
        });
}

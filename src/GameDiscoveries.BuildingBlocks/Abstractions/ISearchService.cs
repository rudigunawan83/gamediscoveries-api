namespace GameDiscoveries.BuildingBlocks.Abstractions;

public interface ISearchService
{
    Task IndexAsync<T>(
        string index,
        IEnumerable<T> documents,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string index,
        string id,
        CancellationToken cancellationToken = default);

    Task<DocumentSearchResult<T>> SearchAsync<T>(
        string index,
        string query,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentSearchResult<T>
{
    public required IReadOnlyList<T> Hits { get; init; }

    public required long EstimatedTotalHits { get; init; }

    public required string Query { get; init; }

    public int? ProcessingTimeMs { get; init; }
}

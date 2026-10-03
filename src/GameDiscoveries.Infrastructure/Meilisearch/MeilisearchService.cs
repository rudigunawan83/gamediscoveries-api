using GameDiscoveries.BuildingBlocks.Abstractions;
using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Meilisearch;

public sealed class MeilisearchService : ISearchService
{
    private readonly MeilisearchClient _client;
    private readonly ILogger<MeilisearchService> _logger;

    public MeilisearchService(
        IOptions<MeilisearchOptions> options,
        ILogger<MeilisearchService> logger)
    {
        var config = options.Value;
        _client = new MeilisearchClient(config.Url, config.ApiKey);
        _logger = logger;
    }

    public async Task IndexAsync<T>(
        string index,
        IEnumerable<T> documents,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var indexClient = _client.Index(index);
            await indexClient.AddDocumentsAsync(documents.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meilisearch index failed for index {IndexName}", index);
            throw;
        }
    }

    public async Task DeleteAsync(
        string index,
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var indexClient = _client.Index(index);
            await indexClient.DeleteOneDocumentAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meilisearch delete failed for index {IndexName} id {DocumentId}", index, id);
            throw;
        }
    }

    public async Task<DocumentSearchResult<T>> SearchAsync<T>(
        string index,
        string query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var indexClient = _client.Index(index);
            var result = await indexClient.SearchAsync<T>(query);
            var hits = result.Hits?.ToList() ?? [];

            return new DocumentSearchResult<T>
            {
                Hits = hits,
                EstimatedTotalHits = hits.Count,
                Query = query,
                ProcessingTimeMs = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meilisearch search failed for index {IndexName} query {Query}", index, query);
            throw;
        }
    }
}

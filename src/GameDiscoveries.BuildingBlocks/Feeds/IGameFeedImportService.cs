namespace GameDiscoveries.BuildingBlocks.Feeds;

public interface IGameFeedImportService
{
    Task<FeedImportResult> ImportAsync(
        GameFeedType feedType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedImportResult>> ImportAllAsync(
        CancellationToken cancellationToken = default);
}

using GameDiscoveries.BuildingBlocks.Feeds;

namespace GameDiscoveries.Modules.Administration.Features.SyncGameFeeds;

public sealed record SyncGameFeedResponse(
    bool Success,
    GameFeedType FeedType,
    int TotalReceived,
    int Created,
    int Updated,
    int Skipped,
    int Failed,
    int Unavailable,
    long DurationMs,
    IReadOnlyList<string> Errors);

public static class SyncGameFeedResponseMapper
{
    public static SyncGameFeedResponse From(FeedImportResult result) => new(
        result.Success,
        result.FeedType,
        result.TotalReceived,
        result.Created,
        result.Updated,
        result.Skipped,
        result.Failed,
        result.Unavailable,
        result.DurationMs,
        result.Errors);
}

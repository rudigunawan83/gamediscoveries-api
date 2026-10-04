namespace GameDiscoveries.BuildingBlocks.Feeds;

public sealed class FeedImportResult
{
    public required GameFeedType FeedType { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }

    public int TotalReceived { get; init; }

    public int Created { get; init; }

    public int Updated { get; init; }

    public int Skipped { get; init; }

    public int Failed { get; init; }

    public int Unavailable { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public long DurationMs => Math.Max(0, (long)(CompletedAt - StartedAt).TotalMilliseconds);

    public bool Success => Failed == 0 && Errors.Count == 0;
}

using System.Text.Json;

namespace GameDiscoveries.Modules.Analytics.Models;

public sealed record AnalyticsEventIngestRequest(
    Guid? EventId,
    string EventType,
    Guid? AnonymousId,
    string? SessionId,
    Guid? GameId,
    string? Source,
    string? Platform,
    string? DeviceType,
    string? AppVersion,
    string? PageUrl,
    string? ReferrerUrl,
    Dictionary<string, object?>? Metadata,
    DateTimeOffset? OccurredAt,
    // Intentionally ignored if present — UserId is resolved from JWT only.
    Guid? UserId = null);

public sealed record AnalyticsEventBatchRequest(IReadOnlyList<AnalyticsEventIngestRequest>? Events);

public sealed record AnalyticsEventIngestResult(
    Guid EventId,
    string Status,
    string? Reason = null);

public sealed record AnalyticsBatchIngestResult(
    int Accepted,
    int Duplicates,
    int Rejected,
    IReadOnlyList<AnalyticsEventIngestResult> Results);

public sealed record AnalyticsEventRecord(
    Guid Id,
    Guid EventId,
    string EventType,
    Guid? UserId,
    Guid? AnonymousId,
    string? SessionId,
    Guid? GameId,
    string Source,
    string Platform,
    string? DeviceType,
    string? AppVersion,
    string? PageUrl,
    string? ReferrerUrl,
    string MetadataJson,
    string? IpHash,
    string? UserAgent,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    DateTimeOffset CreatedAt);

public sealed record AnalyticsEventWriteCommand(
    Guid EventId,
    string EventType,
    Guid? UserId,
    Guid? AnonymousId,
    string? SessionId,
    Guid? GameId,
    string Source,
    string Platform,
    string? DeviceType,
    string? AppVersion,
    string? PageUrl,
    string? ReferrerUrl,
    Dictionary<string, object?> Metadata,
    string? IpHash,
    string? UserAgent,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt)
{
    public string MetadataJson => JsonSerializer.Serialize(Metadata);
}

public sealed record AnalyticsOverviewResponse(
    long TotalEvents,
    long EventsToday,
    long ActiveUsersToday,
    long GameViews,
    long GameStarts,
    long Sessions,
    IReadOnlyList<AnalyticsNamedCount> TopGames,
    IReadOnlyList<AnalyticsNamedCount> TopEventTypes);

public sealed record AnalyticsNamedCount(string Name, long Count);

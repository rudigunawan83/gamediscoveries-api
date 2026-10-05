namespace GameDiscoveries.Modules.Analytics.Models;

public sealed record StartGamePlaySessionRequest(
    Guid? SessionId,
    Guid? AnonymousId,
    string? Source,
    string? Platform,
    string? DeviceType,
    string? AppVersion);

public sealed record HeartbeatGamePlaySessionRequest(
    Guid? AnonymousId,
    DateTimeOffset? ClientTimestamp,
    string? VisibilityState,
    bool? IsFocused);

public sealed record PauseGamePlaySessionRequest(Guid? AnonymousId, string? Reason);

public sealed record ResumeGamePlaySessionRequest(Guid? AnonymousId, string? Reason);

public sealed record EndGamePlaySessionRequest(Guid? AnonymousId, string? Reason);

public sealed record GamePlaySessionResponse(
    Guid SessionId,
    Guid GameId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? PausedAt,
    DateTimeOffset? ResumedAt,
    DateTimeOffset? EndedAt,
    int DurationSeconds,
    int ActiveSeconds,
    bool IsValid,
    string? InvalidReason,
    string Source,
    string Platform);

public sealed record GamePlaySessionOverview(
    long SessionsToday,
    long ActiveSessions,
    long ValidSessions,
    long InvalidSessions,
    double AverageDurationSeconds,
    double AverageActiveSeconds,
    double CompletionRate,
    IReadOnlyList<AnalyticsNamedCount> TopGamesBySessions);

public sealed class GamePlaySessionEntity
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public Guid? AnonymousId { get; set; }
    public Guid GameId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public DateTimeOffset? ResumedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int DurationSeconds { get; set; }
    public int ActiveSeconds { get; set; }
    public long AccumulatedActiveMs { get; set; }
    public bool IsValid { get; set; }
    public string? InvalidReason { get; set; }
    public string Source { get; set; } = "WEB";
    public string Platform { get; set; } = "WEB";
    public string? DeviceType { get; set; }
    public string? AppVersion { get; set; }
    public string? PauseReason { get; set; }
    public string? EndReason { get; set; }
    public DateTimeOffset? LastHeartbeatAnalyticsAt { get; set; }
    public int HeartbeatCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

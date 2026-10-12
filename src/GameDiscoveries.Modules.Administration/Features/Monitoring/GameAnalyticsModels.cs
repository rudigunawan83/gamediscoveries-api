namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

/// <summary>
/// One trend bucket. Players count signed-in users and guests (by anonymous id);
/// playtime is the average server-measured active time of finished sessions.
/// </summary>
public sealed record GamePlaysPoint(
    DateTimeOffset Bucket,
    long Plays,
    long UniquePlayers,
    double? AveragePlaytimeSeconds);

public sealed record GamePlaytimeBucket(string Key, long Sessions);

public sealed record GameAnalyticsTopGame(
    string Title,
    string Slug,
    string? ThumbnailUrl,
    long Plays,
    long UniquePlayers,
    double? AveragePlaytimeSeconds);

public sealed record GameCategoryPlays(string Name, long Plays);

public sealed record GamePlayerMix(long NewPlayers, long ReturningPlayers);

public sealed record GameLaunchFailure(string Title, string Slug, long Attempts, long Failed);

/// <summary>
/// Detail view to valid session. Clicks, loads, and failures come from player events, which
/// exist only from the release that introduced them; sessions are server-measured.
/// LoadSuccessRate is null until any load outcome is reported.
/// </summary>
public sealed record GameLaunchFunnel(
    long Views,
    long PlayClicks,
    long Loaded,
    long LoadFailed,
    long Sessions,
    long ValidSessions,
    double? LoadSuccessRate,
    IReadOnlyList<GameLaunchFailure> FailingGames);

public sealed record MonitoringGamesResponse(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    MonitoringCount TotalGames,
    MonitoringCount TotalPlays,
    MonitoringDuration AveragePlaytime,
    MonitoringCount ActiveGames,
    GameLaunchFunnel Launch,
    IReadOnlyList<GamePlaysPoint> Trend,
    IReadOnlyList<GamePlaytimeBucket> PlaytimeDistribution,
    IReadOnlyList<GameAnalyticsTopGame> TopGames,
    IReadOnlyList<GameCategoryPlays> Categories,
    GamePlayerMix NewVsReturning);

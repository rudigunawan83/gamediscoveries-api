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

public sealed record MonitoringGamesResponse(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    MonitoringCount TotalGames,
    MonitoringCount TotalPlays,
    MonitoringDuration AveragePlaytime,
    MonitoringCount ActiveGames,
    MonitoringUnavailable LaunchOutcomes,
    IReadOnlyList<GamePlaysPoint> Trend,
    IReadOnlyList<GamePlaytimeBucket> PlaytimeDistribution,
    IReadOnlyList<GameAnalyticsTopGame> TopGames,
    IReadOnlyList<GameCategoryPlays> Categories,
    GamePlayerMix NewVsReturning);

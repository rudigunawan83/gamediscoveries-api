using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public interface IGameAnalyticsService
{
    Task<MonitoringGamesResponse> GetGamesAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default);
}

public static class PlaytimeBuckets
{
    public static readonly IReadOnlyList<string> Keys =
        ["lt_1m", "1_5m", "5_10m", "10_30m", "30_60m", "gt_60m"];

    public static IReadOnlyList<GamePlaytimeBucket> From(IReadOnlyList<long> sessions) =>
        Keys.Select((key, index) => new GamePlaytimeBucket(key, index < sessions.Count ? sessions[index] : 0)).ToList();
}

public static class LaunchRates
{
    /// <summary>Share of reported load outcomes that succeeded; null when none were reported.</summary>
    public static double? SuccessPercent(long loaded, long failed)
    {
        var outcomes = loaded + failed;
        return outcomes <= 0 ? null : Math.Round(100.0 * loaded / outcomes, 1);
    }
}

public sealed class GameAnalyticsService(
    IDbConnectionFactory connectionFactory,
    ICacheService cache) : IGameAnalyticsService
{
    private const int TopGamesLimit = 5;
    private const int CategoryLimit = 7;
    private const int FailingGamesLimit = 5;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<MonitoringGamesResponse> GetGamesAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"monitoring:games:v2:{window.Key}";
        if (!bypassCache)
        {
            var cached = await cache.GetAsync<MonitoringGamesResponse>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var games = await LoadAsync(window, cancellationToken);
        await cache.SetAsync(cacheKey, games, CacheTtl, cancellationToken);
        return games;
    }

    private async Task<MonitoringGamesResponse> LoadAsync(
        MonitoringWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var previous = window.Previous;
        var args = new { From = window.From, To = window.To };
        var previousArgs = new { From = previous.From, To = previous.To };

        var catalog = await connection.QuerySingleAsync<CatalogRow>(new CommandDefinition(
            CatalogSql, args, cancellationToken: cancellationToken));
        var current = await connection.QuerySingleAsync<PlayRow>(new CommandDefinition(
            PlaysSql, args, cancellationToken: cancellationToken));
        var before = await connection.QuerySingleAsync<PlayRow>(new CommandDefinition(
            PlaysSql, previousArgs, cancellationToken: cancellationToken));

        var trend = (await connection.QueryAsync<TrendRow>(new CommandDefinition(
            window.Hourly ? HourlyTrendSql : DailyTrendSql,
            args,
            cancellationToken: cancellationToken))).ToList();

        var distribution = await connection.QuerySingleAsync<DistributionRow>(new CommandDefinition(
            DistributionSql, args, cancellationToken: cancellationToken));

        var topGames = (await connection.QueryAsync<TopGameRow>(new CommandDefinition(
            TopGamesSql,
            new { window.From, window.To, Limit = TopGamesLimit },
            cancellationToken: cancellationToken))).ToList();

        var categories = (await connection.QueryAsync<CategoryRow>(new CommandDefinition(
            CategoriesSql,
            new { window.From, window.To, Limit = CategoryLimit },
            cancellationToken: cancellationToken))).ToList();

        var mix = await connection.QuerySingleAsync<MixRow>(new CommandDefinition(
            PlayerMixSql, args, cancellationToken: cancellationToken));

        var funnel = await connection.QuerySingleAsync<FunnelRow>(new CommandDefinition(
            FunnelSql, args, cancellationToken: cancellationToken));
        var failing = (await connection.QueryAsync<GameLaunchFailure>(new CommandDefinition(
            FailingGamesSql,
            new { window.From, window.To, Limit = FailingGamesLimit },
            cancellationToken: cancellationToken))).ToList();

        return new MonitoringGamesResponse(
            window.Key,
            window.From,
            window.To,
            DateTimeOffset.UtcNow,
            new MonitoringCount(catalog.Current, MonitoringMath.ChangePercent(catalog.Current, catalog.Previous)),
            new MonitoringCount(current.Plays, MonitoringMath.ChangePercent(current.Plays, before.Plays)),
            new MonitoringDuration(
                RoundSeconds(current.AverageSeconds),
                MonitoringMath.ChangePercent(current.AverageSeconds, before.AverageSeconds)),
            new MonitoringCount(current.ActiveGames, MonitoringMath.ChangePercent(current.ActiveGames, before.ActiveGames)),
            ToFunnel(funnel, failing),
            trend.Select(row => new GamePlaysPoint(
                AsUtc(row.Bucket),
                row.Plays,
                row.UniquePlayers,
                RoundSeconds(row.AverageSeconds))).ToList(),
            PlaytimeBuckets.From(
            [
                distribution.UnderOneMinute,
                distribution.OneToFive,
                distribution.FiveToTen,
                distribution.TenToThirty,
                distribution.ThirtyToSixty,
                distribution.OverSixty
            ]),
            topGames.Select(row => new GameAnalyticsTopGame(
                row.Title,
                row.Slug,
                row.ThumbnailUrl,
                row.Plays,
                row.UniquePlayers,
                RoundSeconds(row.AverageSeconds))).ToList(),
            categories.Select(row => new GameCategoryPlays(row.Name, row.Plays)).ToList(),
            new GamePlayerMix(mix.NewPlayers, mix.ReturningPlayers));
    }

    private static double? RoundSeconds(double? seconds) =>
        seconds is null ? null : Math.Round(seconds.Value, 1);

    private static GameLaunchFunnel ToFunnel(FunnelRow row, IReadOnlyList<GameLaunchFailure> failing) =>
        new(
            row.Views,
            row.PlayClicks,
            row.Loaded,
            row.LoadFailed,
            row.Sessions,
            row.ValidSessions,
            LaunchRates.SuccessPercent(row.Loaded, row.LoadFailed),
            failing);

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private const string PlayerKey = "COALESCE(user_id::text, anonymous_id::text)";

    private const string CatalogSql =
        """
        SELECT COUNT(*) FILTER (WHERE COALESCE(published_at, created_at) < @To)::bigint AS Current,
               COUNT(*) FILTER (WHERE COALESCE(published_at, created_at) < @From)::bigint AS Previous
        FROM games
        WHERE status = 'published'
        """;

    // Playtime only uses finished sessions so in-progress plays do not drag the average down.
    private const string PlaysSql =
        """
        SELECT COUNT(*)::bigint AS Plays,
               COUNT(DISTINCT game_id)::bigint AS ActiveGames,
               AVG(active_seconds) FILTER (WHERE status = 'ENDED')::double precision AS AverageSeconds
        FROM game_play_sessions
        WHERE started_at >= @From AND started_at < @To
        """;

    private static readonly string DailyTrendSql = TrendSql("day");

    private static readonly string HourlyTrendSql = TrendSql("hour");

    private static string TrendSql(string unit) =>
        $$"""
        WITH buckets AS (
            SELECT generate_series(
                       date_trunc('{{unit}}', @From::timestamptz),
                       date_trunc('{{unit}}', @To::timestamptz - interval '1 microsecond'),
                       interval '1 {{unit}}') AS bucket
        ),
        plays AS (
            SELECT date_trunc('{{unit}}', started_at) AS bucket,
                   COUNT(*)::bigint AS plays,
                   COUNT(DISTINCT {{PlayerKey}})::bigint AS players,
                   AVG(active_seconds) FILTER (WHERE status = 'ENDED')::double precision AS average_seconds
            FROM game_play_sessions
            WHERE started_at >= @From AND started_at < @To
            GROUP BY 1
        )
        SELECT b.bucket AS Bucket,
               COALESCE(p.plays, 0)::bigint AS Plays,
               COALESCE(p.players, 0)::bigint AS UniquePlayers,
               p.average_seconds AS AverageSeconds
        FROM buckets b
        LEFT JOIN plays p ON p.bucket = b.bucket
        ORDER BY b.bucket
        """;

    private const string DistributionSql =
        """
        SELECT COUNT(*) FILTER (WHERE active_seconds < 60)::bigint AS UnderOneMinute,
               COUNT(*) FILTER (WHERE active_seconds >= 60 AND active_seconds < 300)::bigint AS OneToFive,
               COUNT(*) FILTER (WHERE active_seconds >= 300 AND active_seconds < 600)::bigint AS FiveToTen,
               COUNT(*) FILTER (WHERE active_seconds >= 600 AND active_seconds < 1800)::bigint AS TenToThirty,
               COUNT(*) FILTER (WHERE active_seconds >= 1800 AND active_seconds < 3600)::bigint AS ThirtyToSixty,
               COUNT(*) FILTER (WHERE active_seconds >= 3600)::bigint AS OverSixty
        FROM game_play_sessions
        WHERE started_at >= @From AND started_at < @To
          AND status = 'ENDED'
        """;

    private const string TopGamesSql =
        $"""
        WITH ranked AS (
            SELECT game_id,
                   COUNT(*)::bigint AS plays,
                   COUNT(DISTINCT {PlayerKey})::bigint AS players,
                   AVG(active_seconds) FILTER (WHERE status = 'ENDED')::double precision AS average_seconds
            FROM game_play_sessions
            WHERE started_at >= @From AND started_at < @To
            GROUP BY game_id
            ORDER BY plays DESC, game_id
            LIMIT @Limit
        )
        SELECT g.title AS Title,
               g.slug AS Slug,
               g.thumbnail_url AS ThumbnailUrl,
               r.plays AS Plays,
               r.players AS UniquePlayers,
               r.average_seconds AS AverageSeconds
        FROM ranked r
        INNER JOIN games g ON g.id = r.game_id
        ORDER BY r.plays DESC, g.title
        """;

    // A game in several categories counts toward each of them.
    private const string CategoriesSql =
        """
        SELECT c.name AS Name, COUNT(*)::bigint AS Plays
        FROM game_play_sessions s
        INNER JOIN game_categories gc ON gc.game_id = s.game_id
        INNER JOIN categories c ON c.id = gc.category_id
        WHERE s.started_at >= @From AND s.started_at < @To
        GROUP BY c.name
        ORDER BY 2 DESC, 1
        LIMIT @Limit
        """;

    private const string PlayerMixSql =
        """
        WITH players AS (
            SELECT DISTINCT user_id,
                   CASE WHEN user_id IS NULL THEN anonymous_id END AS anonymous_id
            FROM game_play_sessions
            WHERE started_at >= @From AND started_at < @To
              AND (user_id IS NOT NULL OR anonymous_id IS NOT NULL)
        ),
        flagged AS (
            SELECT CASE
                       WHEN p.user_id IS NOT NULL THEN EXISTS (
                           SELECT 1 FROM game_play_sessions e
                           WHERE e.user_id = p.user_id AND e.started_at < @From)
                       ELSE EXISTS (
                           SELECT 1 FROM game_play_sessions e
                           WHERE e.anonymous_id = p.anonymous_id AND e.started_at < @From)
                   END AS is_returning
            FROM players p
        )
        SELECT COUNT(*) FILTER (WHERE NOT is_returning)::bigint AS NewPlayers,
               COUNT(*) FILTER (WHERE is_returning)::bigint AS ReturningPlayers
        FROM flagged
        """;

    private const string FunnelSql =
        """
        WITH events AS (
            SELECT COUNT(*) FILTER (WHERE event_name = 'GAME_VIEW')::bigint AS views,
                   COUNT(*) FILTER (WHERE event_name = 'PLAY_CLICK')::bigint AS play_clicks,
                   COUNT(*) FILTER (WHERE event_name = 'GAME_LOADED')::bigint AS loaded,
                   COUNT(*) FILTER (WHERE event_name = 'GAME_LOAD_FAILED')::bigint AS load_failed
            FROM analytics_events
            WHERE occurred_at >= @From AND occurred_at < @To
              AND event_name IN ('GAME_VIEW', 'PLAY_CLICK', 'GAME_LOADED', 'GAME_LOAD_FAILED')
        ),
        sessions AS (
            SELECT COUNT(*)::bigint AS sessions,
                   COUNT(*) FILTER (WHERE is_valid IS TRUE)::bigint AS valid_sessions
            FROM game_play_sessions
            WHERE started_at >= @From AND started_at < @To
        )
        SELECT e.views AS Views,
               e.play_clicks AS PlayClicks,
               e.loaded AS Loaded,
               e.load_failed AS LoadFailed,
               s.sessions AS Sessions,
               s.valid_sessions AS ValidSessions
        FROM events e CROSS JOIN sessions s
        """;

    private const string FailingGamesSql =
        """
        WITH outcomes AS (
            SELECT game_id,
                   COUNT(*) FILTER (WHERE event_name = 'GAME_LOADED')::bigint AS loaded,
                   COUNT(*) FILTER (WHERE event_name = 'GAME_LOAD_FAILED')::bigint AS failed
            FROM analytics_events
            WHERE occurred_at >= @From AND occurred_at < @To
              AND game_id IS NOT NULL
              AND event_name IN ('GAME_LOADED', 'GAME_LOAD_FAILED')
            GROUP BY game_id
        )
        SELECT g.title AS Title,
               g.slug AS Slug,
               (o.loaded + o.failed)::bigint AS Attempts,
               o.failed AS Failed
        FROM outcomes o
        INNER JOIN games g ON g.id = o.game_id
        WHERE o.failed > 0
        ORDER BY o.failed::double precision / (o.loaded + o.failed) DESC, o.failed DESC, g.title
        LIMIT @Limit
        """;

    private sealed class FunnelRow
    {
        public long Views { get; init; }
        public long PlayClicks { get; init; }
        public long Loaded { get; init; }
        public long LoadFailed { get; init; }
        public long Sessions { get; init; }
        public long ValidSessions { get; init; }
    }

    private sealed class CatalogRow
    {
        public long Current { get; init; }
        public long Previous { get; init; }
    }

    private sealed class PlayRow
    {
        public long Plays { get; init; }
        public long ActiveGames { get; init; }
        public double? AverageSeconds { get; init; }
    }

    private sealed class TrendRow
    {
        public DateTime Bucket { get; init; }
        public long Plays { get; init; }
        public long UniquePlayers { get; init; }
        public double? AverageSeconds { get; init; }
    }

    private sealed class DistributionRow
    {
        public long UnderOneMinute { get; init; }
        public long OneToFive { get; init; }
        public long FiveToTen { get; init; }
        public long TenToThirty { get; init; }
        public long ThirtyToSixty { get; init; }
        public long OverSixty { get; init; }
    }

    private sealed class TopGameRow
    {
        public string Title { get; init; } = "";
        public string Slug { get; init; } = "";
        public string? ThumbnailUrl { get; init; }
        public long Plays { get; init; }
        public long UniquePlayers { get; init; }
        public double? AverageSeconds { get; init; }
    }

    private sealed class CategoryRow
    {
        public string Name { get; init; } = "";
        public long Plays { get; init; }
    }

    private sealed class MixRow
    {
        public long NewPlayers { get; init; }
        public long ReturningPlayers { get; init; }
    }
}

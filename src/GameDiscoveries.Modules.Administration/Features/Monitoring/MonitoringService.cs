using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public interface IMonitoringService
{
    Task<MonitoringOverviewResponse> GetOverviewAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default);
}

public sealed class MonitoringService(
    IDbConnectionFactory connectionFactory,
    ICacheService cache,
    HealthCheckService healthChecks) : IMonitoringService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(90);

    private static readonly MonitoringUnavailable ApiSuccessRate = new(
        "Request success rate is not stored for the dashboard.");

    private static readonly MonitoringUnavailable AverageResponseTime = new(
        "Response time is not stored for the dashboard.");

    private static readonly MonitoringUnavailable EstimatedAdRevenue = new(
        "Ad revenue is not collected.");

    private static readonly MonitoringUnavailable RecentErrors = new(
        "Application error logs are not stored in the database.");

    private static readonly MonitoringUnavailable RecentIncidents = new(
        "Incident tracking is not configured.");

    public async Task<MonitoringOverviewResponse> GetOverviewAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"monitoring:overview:{window.Key}";
        if (!bypassCache)
        {
            var cached = await cache.GetAsync<MonitoringOverviewResponse>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var overview = await LoadAsync(window, cancellationToken);
        await cache.SetAsync(cacheKey, overview, CacheTtl, cancellationToken);
        return overview;
    }

    private async Task<MonitoringOverviewResponse> LoadAsync(
        MonitoringWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var previous = window.Previous;
        var args = new
        {
            From = window.From,
            To = window.To,
            PrevFrom = previous.From
        };

        var plays = await connection.QuerySingleAsync<CountRow>(new CommandDefinition(
            CountSql, args, cancellationToken: cancellationToken));
        var previousPlays = await connection.QuerySingleAsync<CountRow>(new CommandDefinition(
            CountSql,
            new { From = previous.From, To = previous.To },
            cancellationToken: cancellationToken));

        var newUsers = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COUNT(*)::bigint
            FROM users
            WHERE created_at >= @From AND created_at < @To
            """,
            args,
            cancellationToken: cancellationToken));
        var previousUsers = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COUNT(*)::bigint
            FROM users
            WHERE created_at >= @PrevFrom AND created_at < @From
            """,
            args,
            cancellationToken: cancellationToken));

        var series = (await connection.QueryAsync<SeriesRow>(new CommandDefinition(
            window.Hourly ? HourlySeriesSql : DailySeriesSql,
            args,
            cancellationToken: cancellationToken))).ToList();

        var topGames = (await connection.QueryAsync<TopGameRow>(new CommandDefinition(
            TopGamesSql, args, cancellationToken: cancellationToken))).ToList();

        var providers = (await connection.QueryAsync<ProviderRow>(new CommandDefinition(
            """
            SELECT name AS Name,
                   source AS Source,
                   last_sync_status AS Status,
                   last_synced_at AS LastSyncedAt
            FROM game_feeds
            ORDER BY name
            """,
            cancellationToken: cancellationToken))).ToList();

        var health = await SafeHealthAsync(cancellationToken);

        return new MonitoringOverviewResponse(
            window.Key,
            window.From,
            window.To,
            DateTimeOffset.UtcNow,
            new MonitoringCount(plays.ActiveUsers, MonitoringMath.ChangePercent(plays.ActiveUsers, previousPlays.ActiveUsers)),
            new MonitoringCount(plays.Plays, MonitoringMath.ChangePercent(plays.Plays, previousPlays.Plays)),
            new MonitoringCount(newUsers, MonitoringMath.ChangePercent(newUsers, previousUsers)),
            ApiSuccessRate,
            AverageResponseTime,
            EstimatedAdRevenue,
            series.Select(row => new MonitoringPoint(
                AsUtc(row.Bucket),
                row.Plays,
                row.ActiveUsers,
                row.NewUsers,
                row.Dau,
                row.Wau,
                row.Mau)).ToList(),
            topGames.Select(row => new MonitoringTopGame(
                row.Title,
                row.ThumbnailUrl,
                row.Plays,
                MonitoringMath.ChangePercent(row.Plays, row.PreviousPlays))).ToList(),
            health,
            providers.Select(row => new MonitoringProvider(
                row.Name,
                row.Source,
                row.Status,
                row.LastSyncedAt)).ToList(),
            RecentErrors,
            RecentIncidents);
    }

    private async Task<IReadOnlyList<MonitoringComponent>> SafeHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var report = await healthChecks.CheckHealthAsync(cancellationToken);
            return
            [
                Map(report, "self", "API"),
                Map(report, "postgresql", "Database"),
                Map(report, "redis", "Cache"),
                Map(report, "meilisearch", "Search"),
                new MonitoringComponent("Website", "unavailable", "No uptime probe is configured.")
            ];
        }
        catch (Exception)
        {
            return
            [
                new MonitoringComponent("API", "unavailable", "Health checks could not be read."),
                new MonitoringComponent("Database", "unavailable", "Health checks could not be read."),
                new MonitoringComponent("Cache", "unavailable", "Health checks could not be read."),
                new MonitoringComponent("Search", "unavailable", "Health checks could not be read."),
                new MonitoringComponent("Website", "unavailable", "No uptime probe is configured.")
            ];
        }
    }

    private static MonitoringComponent Map(HealthReport report, string name, string label)
    {
        if (!report.Entries.TryGetValue(name, out var entry))
        {
            return new MonitoringComponent(label, "unavailable", "This check is not configured.");
        }

        var status = entry.Status switch
        {
            HealthStatus.Healthy => "healthy",
            HealthStatus.Degraded => "degraded",
            _ => "unhealthy"
        };
        return new MonitoringComponent(label, status, null, Math.Round(entry.Duration.TotalMilliseconds, 1));
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private const string CountSql =
        """
        SELECT COUNT(*)::bigint AS Plays,
               COUNT(DISTINCT user_id) FILTER (WHERE user_id IS NOT NULL)::bigint AS ActiveUsers
        FROM game_play_sessions
        WHERE started_at >= @From AND started_at < @To
        """;

    private static readonly string DailySeriesSql = SeriesSql("day");

    private static readonly string HourlySeriesSql = SeriesSql("hour");

    private static string SeriesSql(string unit) =>
        $$"""
        WITH buckets AS (
            SELECT generate_series(
                       date_trunc('{{unit}}', @From::timestamptz),
                       date_trunc('{{unit}}', @To::timestamptz - interval '1 microsecond'),
                       interval '1 {{unit}}') AS bucket
        ),
        spans AS (
            SELECT bucket,
                   GREATEST(bucket, @From::timestamptz) AS span_start,
                   LEAST(bucket + interval '1 {{unit}}', @To::timestamptz) AS span_end
            FROM buckets
        )
        SELECT s.bucket AS Bucket,
               (SELECT COUNT(*) FROM game_play_sessions p
                WHERE p.started_at >= s.span_start AND p.started_at < s.span_end)::bigint AS Plays,
               (SELECT COUNT(DISTINCT p.user_id) FROM game_play_sessions p
                WHERE p.user_id IS NOT NULL
                  AND p.started_at >= s.span_start AND p.started_at < s.span_end)::bigint AS ActiveUsers,
               (SELECT COUNT(*) FROM users u
                WHERE u.created_at >= s.span_start AND u.created_at < s.span_end)::bigint AS NewUsers,
               (SELECT COUNT(DISTINCT p.user_id) FROM game_play_sessions p
                WHERE p.user_id IS NOT NULL
                  AND p.started_at >= s.span_end - interval '1 day' AND p.started_at < s.span_end)::bigint AS Dau,
               (SELECT COUNT(DISTINCT p.user_id) FROM game_play_sessions p
                WHERE p.user_id IS NOT NULL
                  AND p.started_at >= s.span_end - interval '7 days' AND p.started_at < s.span_end)::bigint AS Wau,
               (SELECT COUNT(DISTINCT p.user_id) FROM game_play_sessions p
                WHERE p.user_id IS NOT NULL
                  AND p.started_at >= s.span_end - interval '30 days' AND p.started_at < s.span_end)::bigint AS Mau
        FROM spans s
        ORDER BY s.bucket
        """;

    private const string TopGamesSql =
        """
        WITH current_games AS (
            SELECT game_id, COUNT(*)::bigint AS plays
            FROM game_play_sessions
            WHERE started_at >= @From AND started_at < @To
            GROUP BY game_id
            ORDER BY plays DESC
            LIMIT 5
        )
        SELECT g.title AS Title,
               g.thumbnail_url AS ThumbnailUrl,
               c.plays AS Plays,
               COALESCE(p.plays, 0)::bigint AS PreviousPlays
        FROM current_games c
        INNER JOIN games g ON g.id = c.game_id
        LEFT JOIN (
            SELECT game_id, COUNT(*)::bigint AS plays
            FROM game_play_sessions
            WHERE started_at >= @PrevFrom AND started_at < @From
              AND game_id IN (SELECT game_id FROM current_games)
            GROUP BY game_id
        ) p ON p.game_id = c.game_id
        ORDER BY c.plays DESC
        """;

    private sealed class CountRow
    {
        public long Plays { get; init; }
        public long ActiveUsers { get; init; }
    }

    private sealed class SeriesRow
    {
        public DateTime Bucket { get; init; }
        public long Plays { get; init; }
        public long ActiveUsers { get; init; }
        public long NewUsers { get; init; }
        public long Dau { get; init; }
        public long Wau { get; init; }
        public long Mau { get; init; }
    }

    private sealed class TopGameRow
    {
        public string Title { get; init; } = "";
        public string? ThumbnailUrl { get; init; }
        public long Plays { get; init; }
        public long PreviousPlays { get; init; }
    }

    private sealed class ProviderRow
    {
        public string Name { get; init; } = "";
        public string Source { get; init; } = "";
        public string? Status { get; init; }
        public DateTimeOffset? LastSyncedAt { get; init; }
    }
}

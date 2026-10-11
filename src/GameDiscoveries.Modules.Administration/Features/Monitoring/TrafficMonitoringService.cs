using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public interface ITrafficMonitoringService
{
    Task<MonitoringTrafficResponse> GetTrafficAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default);
}

public sealed class TrafficMonitoringService(
    IDbConnectionFactory connectionFactory,
    ICacheService cache) : ITrafficMonitoringService
{
    private const int TopLimit = 5;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static readonly MonitoringUnavailable Countries = new(
        "Country is not collected. Only salted IP hashes are stored, and no geo lookup is configured.");

    private static readonly MonitoringUnavailable MobileApp = new(
        "The mobile app does not send page view or search events yet, so these numbers cover the website only.");

    public async Task<MonitoringTrafficResponse> GetTrafficAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"monitoring:traffic:{window.Key}";
        if (!bypassCache)
        {
            var cached = await cache.GetAsync<MonitoringTrafficResponse>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var traffic = await LoadAsync(window, cancellationToken);
        await cache.SetAsync(cacheKey, traffic, CacheTtl, cancellationToken);
        return traffic;
    }

    private async Task<MonitoringTrafficResponse> LoadAsync(
        MonitoringWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var previous = window.Previous;
        var args = new { From = window.From, To = window.To, PrevFrom = previous.From, Limit = TopLimit };
        var previousArgs = new { From = previous.From, To = previous.To };

        var counts = await connection.QuerySingleAsync<CountRow>(new CommandDefinition(
            CountSql, args, cancellationToken: cancellationToken));
        var previousCounts = await connection.QuerySingleAsync<CountRow>(new CommandDefinition(
            CountSql, previousArgs, cancellationToken: cancellationToken));

        var sessions = await connection.QuerySingleAsync<SessionRow>(new CommandDefinition(
            SessionSql, args, cancellationToken: cancellationToken));
        var previousSessions = await connection.QuerySingleAsync<SessionRow>(new CommandDefinition(
            SessionSql, previousArgs, cancellationToken: cancellationToken));

        var mix = await connection.QuerySingleAsync<MixRow>(new CommandDefinition(
            VisitorMixSql, args, cancellationToken: cancellationToken));

        var trend = (await connection.QueryAsync<TrendRow>(new CommandDefinition(
            window.Hourly ? HourlyTrendSql : DailyTrendSql,
            args,
            cancellationToken: cancellationToken))).ToList();

        var landings = (await connection.QueryAsync<LandingRow>(new CommandDefinition(
            LandingSql, args, cancellationToken: cancellationToken))).ToList();

        var landingPages = (await connection.QueryAsync<RankedRow>(new CommandDefinition(
            LandingPagesSql, args, cancellationToken: cancellationToken))).ToList();

        var exitPages = (await connection.QueryAsync<ExitRow>(new CommandDefinition(
            ExitPagesSql, args, cancellationToken: cancellationToken))).ToList();

        var keywords = (await connection.QueryAsync<RankedRow>(new CommandDefinition(
            KeywordsSql, args, cancellationToken: cancellationToken))).ToList();

        return new MonitoringTrafficResponse(
            window.Key,
            window.From,
            window.To,
            DateTimeOffset.UtcNow,
            new MonitoringCount(sessions.Visits, MonitoringMath.ChangePercent(sessions.Visits, previousSessions.Visits)),
            new MonitoringCount(counts.PageViews, MonitoringMath.ChangePercent(counts.PageViews, previousCounts.PageViews)),
            new MonitoringCount(counts.UniqueVisitors, MonitoringMath.ChangePercent(counts.UniqueVisitors, previousCounts.UniqueVisitors)),
            new MonitoringDuration(
                RoundSeconds(sessions.AverageSeconds),
                MonitoringMath.ChangePercent(sessions.AverageSeconds, previousSessions.AverageSeconds)),
            new TrafficVisitorMix(mix.NewVisitors, mix.ReturningVisitors),
            trend.Select(row => new TrafficPoint(AsUtc(row.Bucket), row.Visitors, row.PageViews)).ToList(),
            Shares(landings, row => TrafficClassifier.Source(row.ReferrerHost, row.PageHost, row.UtmMedium, row.HasAdClickId), TrafficClassifier.SourceOrder),
            Countries,
            Shares(landings, row => TrafficClassifier.Device(row.DeviceType), null),
            Shares(landings, row => TrafficClassifier.Browser(row.UserAgent), null),
            landingPages.Select(row => new TrafficPage(
                row.Label,
                row.Hits,
                MonitoringMath.ChangePercent(row.Hits, row.PreviousHits))).ToList(),
            exitPages.Select(row => new TrafficExitPage(
                row.Path,
                row.Exits,
                row.Views > 0 ? Math.Round(row.Exits * 100d / row.Views, 1) : null)).ToList(),
            keywords.Select(row => new TrafficKeyword(
                row.Label,
                row.Hits,
                MonitoringMath.ChangePercent(row.Hits, row.PreviousHits))).ToList(),
            MobileApp);
    }

    private static IReadOnlyList<TrafficShare> Shares(
        IEnumerable<LandingRow> rows,
        Func<LandingRow, string> classify,
        IReadOnlyList<string>? order)
    {
        var totals = rows
            .GroupBy(classify)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Sessions));

        if (order is not null)
        {
            return order.Select(key => new TrafficShare(key, totals.GetValueOrDefault(key))).ToList();
        }

        return totals
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new TrafficShare(pair.Key, pair.Value))
            .ToList();
    }

    private static double? RoundSeconds(double? seconds) =>
        seconds is null ? null : Math.Round(seconds.Value, 1);

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private const string PathExpression =
        "COALESCE(NULLIF(substring(page_url from '^[A-Za-z][A-Za-z0-9+.-]*://[^/?#]+([^?#]*)'), ''), '/')";

    private const string CountSql =
        """
        SELECT COUNT(*) FILTER (WHERE event_name = 'PAGE_VIEW')::bigint AS PageViews,
               COUNT(DISTINCT COALESCE(anonymous_id::text, user_id::text))::bigint AS UniqueVisitors
        FROM analytics_events
        WHERE occurred_at >= @From AND occurred_at < @To
        """;

    private const string SessionSql =
        """
        SELECT COUNT(*)::bigint AS Visits,
               AVG(EXTRACT(EPOCH FROM (last_activity_at - started_at)))::double precision AS AverageSeconds
        FROM analytics_sessions
        WHERE started_at >= @From AND started_at < @To
        """;

    private const string VisitorMixSql =
        """
        WITH visitors AS (
            SELECT DISTINCT anonymous_id
            FROM analytics_events
            WHERE occurred_at >= @From AND occurred_at < @To
              AND anonymous_id IS NOT NULL
        ),
        flagged AS (
            SELECT EXISTS (
                SELECT 1
                FROM analytics_events e
                WHERE e.anonymous_id = v.anonymous_id
                  AND e.occurred_at < @From
            ) AS is_returning
            FROM visitors v
        )
        SELECT COUNT(*) FILTER (WHERE NOT is_returning)::bigint AS NewVisitors,
               COUNT(*) FILTER (WHERE is_returning)::bigint AS ReturningVisitors
        FROM flagged
        """;

    private const string DailyTrendSql =
        """
        SELECT date_trunc('day', occurred_at) AS Bucket,
               COUNT(DISTINCT COALESCE(anonymous_id::text, user_id::text))::bigint AS Visitors,
               COUNT(*) FILTER (WHERE event_name = 'PAGE_VIEW')::bigint AS PageViews
        FROM analytics_events
        WHERE occurred_at >= @From AND occurred_at < @To
        GROUP BY 1
        ORDER BY 1
        """;

    private const string HourlyTrendSql =
        """
        SELECT date_trunc('hour', occurred_at) AS Bucket,
               COUNT(DISTINCT COALESCE(anonymous_id::text, user_id::text))::bigint AS Visitors,
               COUNT(*) FILTER (WHERE event_name = 'PAGE_VIEW')::bigint AS PageViews
        FROM analytics_events
        WHERE occurred_at >= @From AND occurred_at < @To
        GROUP BY 1
        ORDER BY 1
        """;

    private const string LandingSql =
        """
        WITH landings AS (
            SELECT DISTINCT ON (session_id)
                   referrer_url, page_url, device_type, user_agent
            FROM analytics_events
            WHERE occurred_at >= @From AND occurred_at < @To
              AND session_id IS NOT NULL
            ORDER BY session_id, occurred_at
        )
        SELECT lower(substring(referrer_url from '^[A-Za-z][A-Za-z0-9+.-]*://([^/:?#]+)')) AS ReferrerHost,
               lower(substring(page_url from '^[A-Za-z][A-Za-z0-9+.-]*://([^/:?#]+)')) AS PageHost,
               lower(substring(page_url from '[?&]utm_medium=([^&#]+)')) AS UtmMedium,
               COALESCE(page_url ~* '[?&](gclid|gbraid|wbraid|msclkid|dclid)=', false) AS HasAdClickId,
               device_type AS DeviceType,
               user_agent AS UserAgent,
               COUNT(*)::bigint AS Sessions
        FROM landings
        GROUP BY 1, 2, 3, 4, 5, 6
        """;

    private const string LandingPagesSql =
        $"""
        WITH firsts AS (
            SELECT DISTINCT ON (session_id) page_url, occurred_at
            FROM analytics_events
            WHERE event_name = 'PAGE_VIEW'
              AND occurred_at >= @PrevFrom AND occurred_at < @To
              AND session_id IS NOT NULL
              AND page_url IS NOT NULL
            ORDER BY session_id, occurred_at
        ),
        paths AS (
            SELECT left({PathExpression}, 300) AS path,
                   occurred_at >= @From AS is_current
            FROM firsts
        )
        SELECT path AS Label,
               COUNT(*) FILTER (WHERE is_current)::bigint AS Hits,
               COUNT(*) FILTER (WHERE NOT is_current)::bigint AS PreviousHits
        FROM paths
        GROUP BY path
        HAVING COUNT(*) FILTER (WHERE is_current) > 0
        ORDER BY 2 DESC, 1
        LIMIT @Limit
        """;

    private const string ExitPagesSql =
        $"""
        WITH views AS (
            SELECT session_id, occurred_at, left({PathExpression}, 300) AS path
            FROM analytics_events
            WHERE event_name = 'PAGE_VIEW'
              AND occurred_at >= @From AND occurred_at < @To
              AND page_url IS NOT NULL
        ),
        lasts AS (
            SELECT DISTINCT ON (session_id) path
            FROM views
            WHERE session_id IS NOT NULL
            ORDER BY session_id, occurred_at DESC
        ),
        exits AS (
            SELECT path, COUNT(*)::bigint AS exits FROM lasts GROUP BY path
        ),
        totals AS (
            SELECT path, COUNT(*)::bigint AS views FROM views GROUP BY path
        )
        SELECT e.path AS Path, e.exits AS Exits, t.views AS Views
        FROM exits e
        INNER JOIN totals t ON t.path = e.path
        ORDER BY e.exits DESC, e.path
        LIMIT @Limit
        """;

    private const string KeywordsSql =
        """
        WITH searches AS (
            SELECT left(lower(btrim(COALESCE(
                       properties->>'query',
                       properties->>'q',
                       properties->>'searchQuery',
                       properties->>'search'))), 100) AS keyword,
                   occurred_at >= @From AS is_current
            FROM analytics_events
            WHERE event_name = 'SEARCH'
              AND occurred_at >= @PrevFrom AND occurred_at < @To
        )
        SELECT keyword AS Label,
               COUNT(*) FILTER (WHERE is_current)::bigint AS Hits,
               COUNT(*) FILTER (WHERE NOT is_current)::bigint AS PreviousHits
        FROM searches
        WHERE keyword IS NOT NULL AND keyword <> ''
        GROUP BY keyword
        HAVING COUNT(*) FILTER (WHERE is_current) > 0
        ORDER BY 2 DESC, 1
        LIMIT @Limit
        """;

    private sealed class CountRow
    {
        public long PageViews { get; init; }
        public long UniqueVisitors { get; init; }
    }

    private sealed class SessionRow
    {
        public long Visits { get; init; }
        public double? AverageSeconds { get; init; }
    }

    private sealed class MixRow
    {
        public long NewVisitors { get; init; }
        public long ReturningVisitors { get; init; }
    }

    private sealed class TrendRow
    {
        public DateTime Bucket { get; init; }
        public long Visitors { get; init; }
        public long PageViews { get; init; }
    }

    private sealed class LandingRow
    {
        public string? ReferrerHost { get; init; }
        public string? PageHost { get; init; }
        public string? UtmMedium { get; init; }
        public bool HasAdClickId { get; init; }
        public string? DeviceType { get; init; }
        public string? UserAgent { get; init; }
        public long Sessions { get; init; }
    }

    private sealed class RankedRow
    {
        public string Label { get; init; } = "";
        public long Hits { get; init; }
        public long PreviousHits { get; init; }
    }

    private sealed class ExitRow
    {
        public string Path { get; init; } = "";
        public long Exits { get; init; }
        public long Views { get; init; }
    }
}

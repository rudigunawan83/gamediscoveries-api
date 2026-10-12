using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public interface IUserRetentionService
{
    Task<MonitoringUsersResponse> GetUsersAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default);
}

public sealed record UserCohortCell(DateTimeOffset Cohort, int WeekIndex, long Users);

public static class UserRetentionMath
{
    public const int CohortWeeks = 8;

    public static DateTimeOffset WeekStart(DateTimeOffset value)
    {
        var date = value.UtcDateTime.Date;
        var sinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return new DateTimeOffset(date.AddDays(-sinceMonday), TimeSpan.Zero);
    }

    public static double? Percent(long part, long total) =>
        total > 0 ? Math.Round(part * 100d / total, 1) : null;

    public static double? Ratio(long numerator, long denominator) =>
        denominator > 0 ? Math.Round((double)numerator / denominator, 2) : null;

    /// <summary>
    /// The last <see cref="CohortWeeks"/> weekly cohorts, oldest first, each with
    /// week 0 through week <see cref="CohortWeeks"/>.
    /// </summary>
    public static IReadOnlyList<UserCohort> Cohorts(DateTimeOffset now, IEnumerable<UserCohortCell> cells)
    {
        var users = cells.ToDictionary(cell => (cell.Cohort.UtcDateTime, cell.WeekIndex), cell => cell.Users);
        var currentWeek = WeekStart(now);
        var first = currentWeek.AddDays(-7 * (CohortWeeks - 1));

        return Enumerable.Range(0, CohortWeeks).Select(index =>
        {
            var start = first.AddDays(7 * index);
            var size = users.GetValueOrDefault((start.UtcDateTime, 0));
            var retention = Enumerable.Range(0, CohortWeeks + 1)
                .Select(week => start.AddDays(7 * week) > currentWeek
                    ? null
                    : Percent(users.GetValueOrDefault((start.UtcDateTime, week)), size))
                .ToList();
            return new UserCohort(start, size, retention);
        }).ToList();
    }

    public static IReadOnlyList<UserHeatmapCell> Heatmap(IEnumerable<UserHeatmapCell> cells)
    {
        var users = cells.ToDictionary(cell => (cell.Day, cell.Hour), cell => cell.Users);
        return Enumerable.Range(1, 7)
            .SelectMany(day => Enumerable.Range(0, 24)
                .Select(hour => new UserHeatmapCell(day, hour, users.GetValueOrDefault((day, hour)))))
            .ToList();
    }
}

public sealed class UserRetentionService(
    IDbConnectionFactory connectionFactory,
    ICacheService cache) : IUserRetentionService
{
    private const int TopLimit = 5;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<MonitoringUsersResponse> GetUsersAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"monitoring:users:{window.Key}";
        if (!bypassCache)
        {
            var cached = await cache.GetAsync<MonitoringUsersResponse>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var users = await LoadAsync(window, cancellationToken);
        await cache.SetAsync(cacheKey, users, CacheTtl, cancellationToken);
        return users;
    }

    private async Task<MonitoringUsersResponse> LoadAsync(
        MonitoringWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var previous = window.Previous;
        var args = new
        {
            window.From,
            window.To,
            PrevFrom = previous.From,
            EarlierFrom = previous.Previous.From,
            InactiveBefore = window.To.AddDays(-30),
            CohortFrom = UserRetentionMath.WeekStart(window.To).AddDays(-7 * (UserRetentionMath.CohortWeeks - 1)),
            Limit = TopLimit
        };
        var previousArgs = new { From = previous.From, To = previous.To };

        var active = await connection.QuerySingleAsync<ActiveRow>(new CommandDefinition(
            ActiveSql, new { End = window.To }, cancellationToken: cancellationToken));
        var activeBefore = await connection.QuerySingleAsync<ActiveRow>(new CommandDefinition(
            ActiveSql, new { End = window.From }, cancellationToken: cancellationToken));

        var visitors = await connection.QuerySingleAsync<VisitorRow>(new CommandDefinition(
            VisitorsSql, args, cancellationToken: cancellationToken));

        var retained = await connection.QuerySingleAsync<RetainedRow>(new CommandDefinition(
            RetentionRateSql, args, cancellationToken: cancellationToken));

        var activity = (await connection.QueryAsync<ActivityRow>(new CommandDefinition(
            window.Hourly ? HourlyActivitySql : DailyActivitySql,
            args,
            cancellationToken: cancellationToken))).ToList();

        var retention = (await connection.QueryAsync<RetentionRow>(new CommandDefinition(
            RetentionSeriesSql, args, cancellationToken: cancellationToken))).ToList();

        var cohorts = (await connection.QueryAsync<CohortRow>(new CommandDefinition(
            CohortSql, args, cancellationToken: cancellationToken))).ToList();

        var engagement = await connection.QuerySingleAsync<EngagementRow>(new CommandDefinition(
            EngagementSql, args, cancellationToken: cancellationToken));
        var engagementBefore = await connection.QuerySingleAsync<EngagementRow>(new CommandDefinition(
            EngagementSql, previousArgs, cancellationToken: cancellationToken));

        var topGames = (await connection.QueryAsync<TopGameRow>(new CommandDefinition(
            TopGamesSql, args, cancellationToken: cancellationToken))).ToList();

        var heatmap = (await connection.QueryAsync<HeatmapRow>(new CommandDefinition(
            HeatmapSql, args, cancellationToken: cancellationToken))).ToList();

        var acquisition = (await connection.QueryAsync<AcquisitionRow>(new CommandDefinition(
            AcquisitionSql, args, cancellationToken: cancellationToken))).ToList();

        var retentionRate = UserRetentionMath.Percent(retained.Retained, retained.PreviousActive);
        var retentionRateBefore = UserRetentionMath.Percent(retained.EarlierRetained, retained.EarlierActive);

        return new MonitoringUsersResponse(
            window.Key,
            window.From,
            window.To,
            DateTimeOffset.UtcNow,
            Count(active.Dau, activeBefore.Dau),
            Count(active.Wau, activeBefore.Wau),
            Count(active.Mau, activeBefore.Mau),
            new MonitoringRatio(retentionRate, MonitoringMath.ChangePercent(retentionRate, retentionRateBefore)),
            Count(visitors.NewUsers, visitors.PreviousNewUsers),
            activity.Select(row => new UserActivityPoint(
                AsUtc(row.Bucket),
                row.ActiveUsers,
                row.NewUsers,
                row.ActiveUsers - row.NewUsers)).ToList(),
            UserRetentionMath.Cohorts(
                window.To,
                cohorts.Select(row => new UserCohortCell(AsUtc(row.Cohort), row.WeekIndex, row.Users))),
            retention
                .GroupBy(row => row.Day)
                .OrderBy(group => group.Key)
                .Select(group => new UserRetentionPoint(
                    AsUtc(group.Key),
                    LagPercent(group, 1),
                    LagPercent(group, 7),
                    LagPercent(group, 30)))
                .ToList(),
            new UserEngagement(
                Ratio(
                    UserRetentionMath.Ratio(engagement.PlayerGames, engagement.Players),
                    UserRetentionMath.Ratio(engagementBefore.PlayerGames, engagementBefore.Players)),
                new MonitoringDuration(
                    RoundSeconds(engagement.AverageSessionSeconds),
                    MonitoringMath.ChangePercent(engagement.AverageSessionSeconds, engagementBefore.AverageSessionSeconds)),
                Ratio(
                    UserRetentionMath.Ratio(engagement.Sessions, engagement.SessionUsers),
                    UserRetentionMath.Ratio(engagementBefore.Sessions, engagementBefore.SessionUsers)),
                Ratio(
                    UserRetentionMath.Ratio(engagement.Views, engagement.ViewSessions),
                    UserRetentionMath.Ratio(engagementBefore.Views, engagementBefore.ViewSessions))),
            new UserDistribution(visitors.NewUsers, visitors.ReturningUsers, visitors.LapsedUsers, visitors.InactiveUsers),
            topGames.Select(row => new UserTopGame(
                row.Title,
                row.Slug,
                row.ThumbnailUrl,
                row.ActiveUsers,
                UserRetentionMath.Percent(row.RepeatUsers, row.ActiveUsers),
                MonitoringMath.ChangePercent(row.ActiveUsers, row.PreviousUsers))).ToList(),
            UserRetentionMath.Heatmap(heatmap.Select(row => new UserHeatmapCell(row.Day, row.Hour, row.Users))),
            Acquisition(acquisition));
    }

    private static MonitoringCount Count(long current, long previous) =>
        new(current, MonitoringMath.ChangePercent(current, previous));

    private static MonitoringRatio Ratio(double? current, double? previous) =>
        new(current, MonitoringMath.ChangePercent(current, previous));

    private static double? LagPercent(IEnumerable<RetentionRow> rows, int lag)
    {
        var row = rows.FirstOrDefault(item => item.Lag == lag);
        return row is null ? null : UserRetentionMath.Percent(row.Retained, row.Cohort);
    }

    private static IReadOnlyList<UserAcquisitionCohort> Acquisition(IEnumerable<AcquisitionRow> rows) =>
        rows
            .GroupBy(row => TrafficClassifier.IsApp(row.Source)
                ? TrafficClassifier.MobileApp
                : TrafficClassifier.Source(row.ReferrerHost, row.PageHost, row.UtmMedium, row.HasAdClickId))
            .Select(group =>
            {
                var users = group.Sum(row => row.Users);
                return new UserAcquisitionCohort(
                    group.Key,
                    users,
                    UserRetentionMath.Percent(group.Sum(row => row.Returned), group.Sum(row => row.Eligible)),
                    MonitoringMath.ChangePercent(users, group.Sum(row => row.PreviousUsers)));
            })
            .Where(cohort => cohort.Users > 0)
            .OrderByDescending(cohort => cohort.Users)
            .ThenBy(cohort => cohort.Source, StringComparer.Ordinal)
            .Take(TopLimit)
            .ToList();

    private static double? RoundSeconds(double? seconds) =>
        seconds is null ? null : Math.Round(seconds.Value, 1);

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    // A device keeps its anonymous id across sign-in, so it is the stable user key.
    private const string VisitorKey = "COALESCE(anonymous_id::text, user_id::text)";

    private const string IsReal = MonitoringAudience.IsRealActivity;

    private const string HasVisitor = $"((anonymous_id IS NOT NULL OR user_id IS NOT NULL) AND {IsReal})";

    private const string PlayerKey = "COALESCE(user_id::text, anonymous_id::text)";

    private const string IsView = "event_name IN ('PAGE_VIEW', 'SCREEN_VIEW')";

    private const string ActiveSql =
        $"""
        SELECT COUNT(DISTINCT visitor) FILTER (WHERE occurred_at >= @End::timestamptz - interval '1 day')::bigint AS Dau,
               COUNT(DISTINCT visitor) FILTER (WHERE occurred_at >= @End::timestamptz - interval '7 days')::bigint AS Wau,
               COUNT(DISTINCT visitor)::bigint AS Mau
        FROM (
            SELECT {VisitorKey} AS visitor, occurred_at
            FROM analytics_events
            WHERE occurred_at >= @End::timestamptz - interval '30 days' AND occurred_at < @End
              AND {HasVisitor}
        ) e
        """;

    private const string VisitorsSql =
        $"""
        WITH visitors AS (
            SELECT {VisitorKey} AS visitor,
                   MIN(occurred_at) AS first_seen,
                   MAX(occurred_at) AS last_seen
            FROM analytics_events
            WHERE occurred_at < @To AND {HasVisitor}
            GROUP BY 1
        )
        SELECT COUNT(*) FILTER (WHERE first_seen >= @From)::bigint AS NewUsers,
               COUNT(*) FILTER (WHERE first_seen >= @PrevFrom AND first_seen < @From)::bigint AS PreviousNewUsers,
               COUNT(*) FILTER (WHERE first_seen < @From AND last_seen >= @From)::bigint AS ReturningUsers,
               COUNT(*) FILTER (WHERE last_seen < @From AND last_seen >= @InactiveBefore)::bigint AS LapsedUsers,
               COUNT(*) FILTER (WHERE last_seen < @InactiveBefore)::bigint AS InactiveUsers
        FROM visitors
        """;

    // Retention rate: users active in the previous window who came back in this one.
    private const string RetentionRateSql =
        $"""
        WITH seen AS (
            SELECT DISTINCT {VisitorKey} AS visitor,
                   CASE WHEN occurred_at >= @From THEN 2
                        WHEN occurred_at >= @PrevFrom THEN 1
                        ELSE 0 END AS period
            FROM analytics_events
            WHERE occurred_at >= @EarlierFrom AND occurred_at < @To AND {HasVisitor}
        ),
        flags AS (
            SELECT visitor,
                   bool_or(period = 0) AS earlier,
                   bool_or(period = 1) AS previous,
                   bool_or(period = 2) AS current
            FROM seen
            GROUP BY visitor
        )
        SELECT COUNT(*) FILTER (WHERE previous)::bigint AS PreviousActive,
               COUNT(*) FILTER (WHERE previous AND current)::bigint AS Retained,
               COUNT(*) FILTER (WHERE earlier)::bigint AS EarlierActive,
               COUNT(*) FILTER (WHERE earlier AND previous)::bigint AS EarlierRetained
        FROM flags
        """;

    private static readonly string DailyActivitySql = ActivitySql("day");

    private static readonly string HourlyActivitySql = ActivitySql("hour");

    private static string ActivitySql(string unit) =>
        $$"""
        WITH buckets AS (
            SELECT generate_series(
                       date_trunc('{{unit}}', @From::timestamptz),
                       date_trunc('{{unit}}', @To::timestamptz - interval '1 microsecond'),
                       interval '1 {{unit}}') AS bucket
        ),
        active AS (
            SELECT DISTINCT date_trunc('{{unit}}', occurred_at) AS bucket, {{VisitorKey}} AS visitor
            FROM analytics_events
            WHERE occurred_at >= @From AND occurred_at < @To AND {{HasVisitor}}
        ),
        firsts AS (
            SELECT {{VisitorKey}} AS visitor, MIN(occurred_at) AS first_seen
            FROM analytics_events
            WHERE occurred_at < @To AND {{HasVisitor}}
            GROUP BY 1
        )
        SELECT b.bucket AS Bucket,
               COUNT(a.visitor)::bigint AS ActiveUsers,
               COUNT(a.visitor) FILTER (WHERE f.first_seen >= b.bucket)::bigint AS NewUsers
        FROM buckets b
        LEFT JOIN active a ON a.bucket = b.bucket
        LEFT JOIN firsts f ON f.visitor = a.visitor
        GROUP BY b.bucket
        ORDER BY b.bucket
        """;

    // For each day, the cohorts first seen 1, 7, and 30 days earlier and how many were active that day.
    private const string RetentionSeriesSql =
        $"""
        WITH days AS (
            SELECT generate_series(
                       date_trunc('day', @From::timestamptz),
                       date_trunc('day', @To::timestamptz - interval '1 microsecond'),
                       interval '1 day') AS day
        ),
        lags AS (
            SELECT unnest(ARRAY[1, 7, 30]) AS lag
        ),
        firsts AS (
            SELECT {VisitorKey} AS visitor, date_trunc('day', MIN(occurred_at)) AS first_day
            FROM analytics_events
            WHERE occurred_at < @To AND {HasVisitor}
            GROUP BY 1
            HAVING MIN(occurred_at) >= date_trunc('day', @From::timestamptz) - interval '30 days'
        ),
        visitor_days AS (
            SELECT DISTINCT {VisitorKey} AS visitor, date_trunc('day', occurred_at) AS day
            FROM analytics_events
            WHERE occurred_at >= date_trunc('day', @From::timestamptz) AND occurred_at < @To AND {HasVisitor}
        )
        SELECT d.day AS Day,
               l.lag AS Lag,
               COUNT(f.visitor)::bigint AS Cohort,
               COUNT(v.visitor)::bigint AS Retained
        FROM days d
        CROSS JOIN lags l
        LEFT JOIN firsts f ON f.first_day = d.day - make_interval(days => l.lag)
        LEFT JOIN visitor_days v ON v.visitor = f.visitor AND v.day = d.day
        GROUP BY d.day, l.lag
        ORDER BY d.day, l.lag
        """;

    private const string CohortSql =
        $"""
        WITH firsts AS (
            SELECT {VisitorKey} AS visitor, date_trunc('week', MIN(occurred_at)) AS cohort
            FROM analytics_events
            WHERE occurred_at < @To AND {HasVisitor}
            GROUP BY 1
            HAVING MIN(occurred_at) >= @CohortFrom
        ),
        weeks AS (
            SELECT DISTINCT {VisitorKey} AS visitor, date_trunc('week', occurred_at) AS week
            FROM analytics_events
            WHERE occurred_at >= @CohortFrom AND occurred_at < @To AND {HasVisitor}
        )
        SELECT f.cohort AS Cohort,
               round(EXTRACT(EPOCH FROM (w.week - f.cohort)) / 604800)::int AS WeekIndex,
               COUNT(*)::bigint AS Users
        FROM firsts f
        INNER JOIN weeks w ON w.visitor = f.visitor AND w.week >= f.cohort
        GROUP BY 1, 2
        ORDER BY 1, 2
        """;

    private const string EngagementSql =
        $"""
        SELECT (SELECT COUNT(*) FROM analytics_sessions
                WHERE started_at >= @From AND started_at < @To AND {IsReal})::bigint AS Sessions,
               (SELECT COUNT(DISTINCT {VisitorKey}) FROM analytics_sessions
                WHERE started_at >= @From AND started_at < @To AND {IsReal})::bigint AS SessionUsers,
               (SELECT AVG(EXTRACT(EPOCH FROM (last_activity_at - started_at))) FROM analytics_sessions
                WHERE started_at >= @From AND started_at < @To AND {IsReal})::double precision AS AverageSessionSeconds,
               (SELECT COUNT(*) FROM analytics_events
                WHERE {IsView} AND session_id IS NOT NULL
                  AND occurred_at >= @From AND occurred_at < @To AND {IsReal})::bigint AS Views,
               (SELECT COUNT(DISTINCT session_id) FROM analytics_events
                WHERE {IsView} AND session_id IS NOT NULL
                  AND occurred_at >= @From AND occurred_at < @To AND {IsReal})::bigint AS ViewSessions,
               (SELECT COUNT(DISTINCT {PlayerKey} || ':' || game_id::text) FROM game_play_sessions
                WHERE started_at >= @From AND started_at < @To AND {IsReal})::bigint AS PlayerGames,
               (SELECT COUNT(DISTINCT {PlayerKey}) FROM game_play_sessions
                WHERE started_at >= @From AND started_at < @To AND {IsReal})::bigint AS Players
        """;

    // Repeat players played the game on more than one UTC day in the range.
    private const string TopGamesSql =
        $"""
        WITH player_days AS (
            SELECT game_id,
                   {PlayerKey} AS player,
                   COUNT(DISTINCT date_trunc('day', started_at)) AS days
            FROM game_play_sessions
            WHERE started_at >= @From AND started_at < @To
              AND (user_id IS NOT NULL OR anonymous_id IS NOT NULL)
              AND {IsReal}
            GROUP BY 1, 2
        ),
        ranked AS (
            SELECT game_id,
                   COUNT(*)::bigint AS players,
                   COUNT(*) FILTER (WHERE days > 1)::bigint AS repeat_players
            FROM player_days
            GROUP BY game_id
            ORDER BY players DESC, game_id
            LIMIT @Limit
        ),
        previous AS (
            SELECT game_id, COUNT(DISTINCT {PlayerKey})::bigint AS players
            FROM game_play_sessions
            WHERE started_at >= @PrevFrom AND started_at < @From
              AND game_id IN (SELECT game_id FROM ranked)
              AND {IsReal}
            GROUP BY game_id
        )
        SELECT g.title AS Title,
               g.slug AS Slug,
               g.thumbnail_url AS ThumbnailUrl,
               r.players AS ActiveUsers,
               r.repeat_players AS RepeatUsers,
               COALESCE(p.players, 0)::bigint AS PreviousUsers
        FROM ranked r
        INNER JOIN games g ON g.id = r.game_id
        LEFT JOIN previous p ON p.game_id = r.game_id
        ORDER BY r.players DESC, g.title
        """;

    private const string HeatmapSql =
        $"""
        SELECT EXTRACT(ISODOW FROM occurred_at AT TIME ZONE 'UTC')::int AS Day,
               EXTRACT(HOUR FROM occurred_at AT TIME ZONE 'UTC')::int AS Hour,
               COUNT(DISTINCT {VisitorKey})::bigint AS Users
        FROM analytics_events
        WHERE occurred_at >= @From AND occurred_at < @To AND {HasVisitor}
        GROUP BY 1, 2
        """;

    // New users are grouped by the first event they ever sent; returns are counted on days 1-7 after it.
    private const string AcquisitionSql =
        $"""
        WITH firsts AS (
            SELECT DISTINCT ON (visitor) visitor, occurred_at, referrer_url, page_url, source
            FROM (
                SELECT {VisitorKey} AS visitor, occurred_at, referrer_url, page_url, source
                FROM analytics_events
                WHERE occurred_at < @To AND {HasVisitor}
            ) e
            ORDER BY visitor, occurred_at
        ),
        newcomers AS (
            SELECT visitor,
                   date_trunc('day', occurred_at) AS first_day,
                   occurred_at >= @From AS is_current,
                   date_trunc('day', occurred_at) + interval '8 days' <= @To AS eligible,
                   referrer_url, page_url, source
            FROM firsts
            WHERE occurred_at >= @PrevFrom
        ),
        visitor_days AS (
            SELECT DISTINCT {VisitorKey} AS visitor, date_trunc('day', occurred_at) AS day
            FROM analytics_events
            WHERE occurred_at >= @From AND occurred_at < @To AND {HasVisitor}
        ),
        returns AS (
            SELECT n.visitor
            FROM newcomers n
            INNER JOIN visitor_days d ON d.visitor = n.visitor
            WHERE n.is_current
              AND d.day > n.first_day
              AND d.day <= n.first_day + interval '7 days'
            GROUP BY n.visitor
        )
        SELECT lower(substring(n.referrer_url from '^[A-Za-z][A-Za-z0-9+.-]*://([^/:?#]+)')) AS ReferrerHost,
               lower(substring(n.page_url from '^[A-Za-z][A-Za-z0-9+.-]*://([^/:?#]+)')) AS PageHost,
               lower(substring(n.page_url from '[?&]utm_medium=([^&#]+)')) AS UtmMedium,
               COALESCE(n.page_url ~* '[?&](gclid|gbraid|wbraid|msclkid|dclid)=', false) AS HasAdClickId,
               n.source AS Source,
               COUNT(*) FILTER (WHERE n.is_current)::bigint AS Users,
               COUNT(*) FILTER (WHERE NOT n.is_current)::bigint AS PreviousUsers,
               COUNT(*) FILTER (WHERE n.is_current AND n.eligible)::bigint AS Eligible,
               COUNT(*) FILTER (WHERE n.is_current AND n.eligible AND r.visitor IS NOT NULL)::bigint AS Returned
        FROM newcomers n
        LEFT JOIN returns r ON r.visitor = n.visitor
        GROUP BY 1, 2, 3, 4, 5
        """;

    private sealed class ActiveRow
    {
        public long Dau { get; init; }
        public long Wau { get; init; }
        public long Mau { get; init; }
    }

    private sealed class VisitorRow
    {
        public long NewUsers { get; init; }
        public long PreviousNewUsers { get; init; }
        public long ReturningUsers { get; init; }
        public long LapsedUsers { get; init; }
        public long InactiveUsers { get; init; }
    }

    private sealed class RetainedRow
    {
        public long PreviousActive { get; init; }
        public long Retained { get; init; }
        public long EarlierActive { get; init; }
        public long EarlierRetained { get; init; }
    }

    private sealed class ActivityRow
    {
        public DateTime Bucket { get; init; }
        public long ActiveUsers { get; init; }
        public long NewUsers { get; init; }
    }

    private sealed class RetentionRow
    {
        public DateTime Day { get; init; }
        public int Lag { get; init; }
        public long Cohort { get; init; }
        public long Retained { get; init; }
    }

    private sealed class CohortRow
    {
        public DateTime Cohort { get; init; }
        public int WeekIndex { get; init; }
        public long Users { get; init; }
    }

    private sealed class EngagementRow
    {
        public long Sessions { get; init; }
        public long SessionUsers { get; init; }
        public double? AverageSessionSeconds { get; init; }
        public long Views { get; init; }
        public long ViewSessions { get; init; }
        public long PlayerGames { get; init; }
        public long Players { get; init; }
    }

    private sealed class TopGameRow
    {
        public string Title { get; init; } = "";
        public string Slug { get; init; } = "";
        public string? ThumbnailUrl { get; init; }
        public long ActiveUsers { get; init; }
        public long RepeatUsers { get; init; }
        public long PreviousUsers { get; init; }
    }

    private sealed class HeatmapRow
    {
        public int Day { get; init; }
        public int Hour { get; init; }
        public long Users { get; init; }
    }

    private sealed class AcquisitionRow
    {
        public string? ReferrerHost { get; init; }
        public string? PageHost { get; init; }
        public string? UtmMedium { get; init; }
        public bool HasAdClickId { get; init; }
        public string? Source { get; init; }
        public long Users { get; init; }
        public long PreviousUsers { get; init; }
        public long Eligible { get; init; }
        public long Returned { get; init; }
    }
}

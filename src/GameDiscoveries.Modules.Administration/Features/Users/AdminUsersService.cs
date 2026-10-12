using System.Data;
using System.Data.Common;
using System.Text;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Administration.Features.Monitoring;

namespace GameDiscoveries.Modules.Administration.Features.Users;

public interface IAdminUsersService
{
    Task<AdminUsersPage> ListAsync(AdminUsersFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AdminUsersStats> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(AdminUsersFilter filter, CancellationToken cancellationToken = default);
}

public sealed class AdminUsersService(IDbConnectionFactory connectionFactory, TimeProvider clock) : IAdminUsersService
{
    private const int TrendDays = 30;
    private const int ActiveTrendDays = 14;
    private const int ActiveBaselineDays = 7;

    public async Task<AdminUsersPage> ListAsync(
        AdminUsersFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, AdminUsersQuery.MaxPageSize);

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<UserRow>(new CommandDefinition(
            DirectorySql(filter),
            Parameters(filter, pageSize, (page - 1) * pageSize),
            cancellationToken: cancellationToken))).ToList();

        var total = rows.Count > 0 ? rows[0].Total : await CountAsync(connection, filter, cancellationToken);
        return new AdminUsersPage(rows.Select(ToRow).ToList(), page, pageSize, total);
    }

    public async Task<string> ExportCsvAsync(AdminUsersFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserRow>(new CommandDefinition(
            DirectorySql(filter),
            Parameters(filter, AdminUsersQuery.MaxExportRows, 0),
            cancellationToken: cancellationToken));

        var csv = new StringBuilder("id,name,username,email,role,status,last_active_at,joined_at,games_played,level,total_xp\n");
        foreach (var row in rows.Select(ToRow))
        {
            csv.AppendJoin(',',
                row.Id.ToString(),
                AdminUsersQuery.CsvCell(row.DisplayName),
                AdminUsersQuery.CsvCell(row.Username),
                AdminUsersQuery.CsvCell(row.Email),
                row.Role,
                row.Status,
                row.LastActiveAt?.ToString("O") ?? string.Empty,
                row.CreatedAt.ToString("O"),
                row.GamesPlayed.ToString(),
                row.Level.ToString(),
                row.TotalXp.ToString());
            csv.Append('\n');
        }

        return csv.ToString();
    }

    public async Task<AdminUsersStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var previousMonthStart = monthStart.AddMonths(-1);
        var previousMonthEnd = previousMonthStart + (now - monthStart);

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var totals = await connection.QuerySingleAsync<TotalsRow>(new CommandDefinition(
            TotalsSql,
            new { Now = now, MonthStart = monthStart, PreviousMonthStart = previousMonthStart, PreviousMonthEnd = previousMonthEnd },
            cancellationToken: cancellationToken));

        var dailyNew = await DailyAsync(connection, NewUsersByDaySql, now, TrendDays, cancellationToken);
        var dailyActive = await DailyAsync(connection, ActiveUsersByDaySql, now, ActiveTrendDays, cancellationToken);

        var totalTrend = AdminUsersQuery.CumulativeTrend(totals.Total, dailyNew);
        var totalMonthAgo = totals.Total - dailyNew.Sum();

        return new AdminUsersStats(
            new AdminUsersStat(totals.Total, MonitoringMath.ChangePercent(totals.Total, totalMonthAgo), totalTrend),
            new AdminUsersStat(
                dailyActive[0],
                AdminUsersQuery.ChangeVsAverage(dailyActive[0], dailyActive.Skip(1).Take(ActiveBaselineDays).ToList()),
                Enumerable.Reverse(dailyActive).ToList()),
            new AdminUsersStat(
                totals.NewThisMonth,
                MonitoringMath.ChangePercent(totals.NewThisMonth, totals.NewPreviousMonth),
                Enumerable.Reverse(dailyNew).ToList()),
            totals.Suspended,
            UserRetentionMath.Percent(totals.Suspended, totals.Total),
            now);
    }

    /// <summary>Values per day, newest first; index 0 is the 24 hours ending now.</summary>
    private static async Task<long[]> DailyAsync(
        DbConnection connection,
        string sql,
        DateTimeOffset now,
        int days,
        CancellationToken cancellationToken)
    {
        var values = new long[days];
        var rows = await connection.QueryAsync<DayRow>(new CommandDefinition(
            sql,
            new { Now = now, Since = now.AddDays(-days) },
            cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            if (row.Day >= 0 && row.Day < days) values[row.Day] = row.Value;
        }

        return values;
    }

    private static async Task<long> CountAsync(DbConnection connection, AdminUsersFilter filter, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT COUNT(*) FROM ({BaseSql}) base WHERE {RoleFilter}",
            Parameters(filter, 0, 0),
            cancellationToken: cancellationToken));

    private static DynamicParameters Parameters(AdminUsersFilter filter, int limit, int offset)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Search", filter.Search is null ? null : $"%{filter.Search}%", DbType.String);
        parameters.Add("Status", filter.Status, DbType.String);
        parameters.Add("Role", filter.Role, DbType.String);
        parameters.Add("JoinedFrom", filter.JoinedFrom, DbType.DateTimeOffset);
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);
        return parameters;
    }

    private static string DirectorySql(AdminUsersFilter filter)
    {
        var direction = filter.Descending ? "DESC" : "ASC";
        return $"""
            SELECT id AS Id, email AS Email, display_name AS DisplayName, username AS Username,
                   avatar_url AS AvatarUrl, role AS Role, status AS Status, last_active_at AS LastActiveAt,
                   created_at AS CreatedAt, games_played AS GamesPlayed, level AS Level, total_xp AS TotalXp,
                   COUNT(*) OVER () AS Total
            FROM ({BaseSql}) base
            WHERE {RoleFilter}
            ORDER BY {filter.SortColumn} {direction} NULLS LAST, created_at DESC, id
            LIMIT @Limit OFFSET @Offset
            """;
    }

    // Untyped null parameters cannot be inferred by PostgreSQL (42P08), so every filter is cast.
    private const string RoleFilter = "(@Role::text IS NULL OR role = @Role::text)";

    // Last active is the latest of tracked events, play sessions, and gamification activity.
    private const string BaseSql =
        """
        SELECT u.id,
               u.email,
               COALESCE(NULLIF(u.display_name, ''), u.username, split_part(u.email, '@', 1)) AS display_name,
               u.username,
               u.avatar_url,
               COALESCE(r.role, 'Player') AS role,
               u.status,
               GREATEST(p.last_activity_at, ev.last_at, gs.last_at) AS last_active_at,
               u.created_at,
               COALESCE(gs.games, 0)::int AS games_played,
               COALESCE(p.level, 1) AS level,
               COALESCE(p.total_xp, 0)::bigint AS total_xp
        FROM users u
        LEFT JOIN user_progress p ON p.user_id = u.id
        LEFT JOIN LATERAL (
            SELECT ur.role FROM user_roles ur
            WHERE ur.user_id = u.id
            ORDER BY array_position(ARRAY['SuperAdmin','Admin','Moderator','Developer','Advertiser','Player']::text[], ur.role::text) NULLS LAST
            LIMIT 1
        ) r ON TRUE
        LEFT JOIN LATERAL (
            SELECT MAX(e.created_at) AS last_at FROM analytics_events e WHERE e.user_id = u.id
        ) ev ON TRUE
        LEFT JOIN LATERAL (
            SELECT MAX(s.started_at) AS last_at,
                   COUNT(DISTINCT s.game_id) FILTER (WHERE s.is_valid) AS games
            FROM game_play_sessions s WHERE s.user_id = u.id
        ) gs ON TRUE
        WHERE (@Search::text IS NULL OR u.email ILIKE @Search::text OR u.display_name ILIKE @Search::text
               OR u.username ILIKE @Search::text OR u.id::text ILIKE @Search::text)
          AND (@Status::text IS NULL OR u.status = @Status::text)
          AND (@JoinedFrom::timestamptz IS NULL OR u.created_at >= @JoinedFrom::timestamptz)
        """;

    private const string TotalsSql =
        """
        SELECT COUNT(*)::bigint AS Total,
               COUNT(*) FILTER (WHERE status = 'suspended')::bigint AS Suspended,
               COUNT(*) FILTER (WHERE created_at >= @MonthStart AND created_at < @Now)::bigint AS NewThisMonth,
               COUNT(*) FILTER (WHERE created_at >= @PreviousMonthStart AND created_at < @PreviousMonthEnd)::bigint AS NewPreviousMonth
        FROM users
        """;

    private const string NewUsersByDaySql =
        """
        SELECT floor(extract(epoch FROM (@Now - created_at)) / 86400)::int AS Day, COUNT(*)::bigint AS Value
        FROM users
        WHERE created_at > @Since AND created_at <= @Now
        GROUP BY 1
        """;

    private const string ActiveUsersByDaySql =
        """
        SELECT floor(extract(epoch FROM (@Now - at)) / 86400)::int AS Day, COUNT(DISTINCT user_id)::bigint AS Value
        FROM (
            SELECT user_id, created_at AS at FROM analytics_events
            WHERE user_id IS NOT NULL AND created_at > @Since AND created_at <= @Now
            UNION ALL
            SELECT user_id, started_at FROM game_play_sessions
            WHERE user_id IS NOT NULL AND started_at > @Since AND started_at <= @Now
        ) activity
        GROUP BY 1
        """;

    private static AdminUserRow ToRow(UserRow row) =>
        new(
            row.Id,
            row.Email,
            row.DisplayName,
            row.Username,
            row.AvatarUrl,
            row.Role,
            row.Status,
            row.LastActiveAt is { } lastActive ? AsUtc(lastActive) : null,
            AsUtc(row.CreatedAt),
            row.GamesPlayed,
            row.Level,
            row.TotalXp);

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class UserRow
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Username { get; init; }
        public string? AvatarUrl { get; init; }
        public string Role { get; init; } = "Player";
        public string Status { get; init; } = "active";
        public DateTime? LastActiveAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public int GamesPlayed { get; init; }
        public int Level { get; init; }
        public long TotalXp { get; init; }
        public long Total { get; init; }
    }

    private sealed class TotalsRow
    {
        public long Total { get; init; }
        public long Suspended { get; init; }
        public long NewThisMonth { get; init; }
        public long NewPreviousMonth { get; init; }
    }

    private sealed class DayRow
    {
        public int Day { get; init; }
        public long Value { get; init; }
    }
}

using GameDiscoveries.Modules.Administration.Features.Monitoring;

namespace GameDiscoveries.Modules.Administration.Features.Users;

public sealed record AdminUserRow(
    Guid Id,
    string Email,
    string DisplayName,
    string? Username,
    string? AvatarUrl,
    string Role,
    string Status,
    DateTimeOffset? LastActiveAt,
    DateTimeOffset CreatedAt,
    int GamesPlayed,
    int Level,
    long TotalXp);

public sealed record AdminUsersPage(
    IReadOnlyList<AdminUserRow> Items,
    int Page,
    int PageSize,
    long Total);

/// <param name="Trend">Oldest first, one point per day.</param>
public sealed record AdminUsersStat(long Value, double? ChangePercent, IReadOnlyList<long> Trend);

/// <summary>
/// Total users compares with the total 30 days ago, active users (last 24 hours) with the daily
/// average of the previous 7 days, and new users this month with the same span of last month.
/// </summary>
public sealed record AdminUsersStats(
    AdminUsersStat TotalUsers,
    AdminUsersStat ActiveToday,
    AdminUsersStat NewThisMonth,
    long Suspended,
    double? SuspendedPercent,
    DateTimeOffset GeneratedAt);

public sealed record AdminUsersFilter(
    string? Search,
    string? Status,
    string? Role,
    DateTimeOffset? JoinedFrom,
    string SortColumn,
    bool Descending);

public static class AdminUsersQuery
{
    public const int MaxPageSize = 100;
    public const int MaxExportRows = 10_000;

    public static readonly IReadOnlyList<string> Statuses = ["active", "suspended"];

    public static readonly IReadOnlyList<string> RoleOrder =
        ["SuperAdmin", "Admin", "Moderator", "Developer", "Advertiser", "Player"];

    private static readonly IReadOnlyDictionary<string, string> SortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lastActive"] = "last_active_at",
            ["joined"] = "created_at",
            ["games"] = "games_played",
            ["name"] = "lower(display_name)",
            ["xp"] = "total_xp",
        };

    public static AdminUsersFilter Parse(
        string? search,
        string? status,
        string? role,
        string? joined,
        string? sort,
        string? dir,
        DateTimeOffset now)
    {
        var normalizedStatus = Statuses.FirstOrDefault(s => s.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));
        var normalizedRole = RoleOrder.FirstOrDefault(r => r.Equals(role?.Trim(), StringComparison.OrdinalIgnoreCase));
        var column = SortColumns.TryGetValue(sort?.Trim() ?? string.Empty, out var mapped) ? mapped : "created_at";
        var descending = !"asc".Equals(dir?.Trim(), StringComparison.OrdinalIgnoreCase);
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        return new AdminUsersFilter(term, normalizedStatus, normalizedRole, JoinedFrom(joined, now), column, descending);
    }

    public static DateTimeOffset? JoinedFrom(string? joined, DateTimeOffset now) => joined?.Trim().ToLowerInvariant() switch
    {
        "24h" => now.AddDays(-1),
        "7d" => now.AddDays(-7),
        "30d" => now.AddDays(-30),
        "90d" => now.AddDays(-90),
        "365d" => now.AddDays(-365),
        _ => null,
    };

    /// <summary>Change of the latest value against the average of the earlier ones.</summary>
    public static double? ChangeVsAverage(long current, IReadOnlyList<long> previous) =>
        previous.Count == 0 ? null : MonitoringMath.ChangePercent(current, previous.Average());

    /// <summary>Running totals for each day, oldest first, ending at <paramref name="total"/>.</summary>
    /// <param name="dailyNew">New users per day, newest first (index 0 is the last 24 hours).</param>
    public static IReadOnlyList<long> CumulativeTrend(long total, IReadOnlyList<long> dailyNew)
    {
        var points = new long[dailyNew.Count];
        var running = total;
        for (var i = 0; i < dailyNew.Count; i++)
        {
            points[dailyNew.Count - 1 - i] = running;
            running -= dailyNew[i];
        }

        return points;
    }

    /// <summary>Quotes a CSV cell and neutralizes spreadsheet formulas.</summary>
    public static string CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

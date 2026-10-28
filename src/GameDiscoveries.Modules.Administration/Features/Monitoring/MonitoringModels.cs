namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public readonly record struct MonitoringWindow(
    string Key,
    DateTimeOffset From,
    DateTimeOffset To,
    bool Hourly)
{
    public TimeSpan Length => To - From;

    public MonitoringWindow Previous => new(Key, From - Length, From, Hourly);

    public const int MaxCustomDays = 90;

    public static MonitoringWindow Parse(string? raw, DateTimeOffset now) =>
        Parse(raw, null, null, now);

    /// <summary>
    /// Presets: 24h, 7d, 30d. Custom: pass <paramref name="from"/> and <paramref name="to"/> as
    /// calendar dates (yyyy-MM-dd); both days are inclusive and To is exclusive at the next midnight UTC.
    /// </summary>
    public static MonitoringWindow Parse(string? raw, string? from, string? to, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
        {
            return Custom(from, to, now);
        }

        var key = string.IsNullOrWhiteSpace(raw) ? "30d" : raw.Trim().ToLowerInvariant();
        return key switch
        {
            "24h" => new MonitoringWindow(key, now.AddHours(-24), now, true),
            "7d" => new MonitoringWindow(key, now.AddDays(-7), now, false),
            "30d" => new MonitoringWindow(key, now.AddDays(-30), now, false),
            "custom" => throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException(
                "Custom range requires from and to query parameters (yyyy-MM-dd)."),
            _ => throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException(
                "Range must be 24h, 7d, 30d, or custom with from and to.")
        };
    }

    private static MonitoringWindow Custom(string? fromRaw, string? toRaw, DateTimeOffset now)
    {
        if (!TryParseDay(fromRaw, out var fromDay) || !TryParseDay(toRaw, out var toDay))
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException(
                "from and to must be calendar dates in yyyy-MM-dd.");
        }

        if (toDay < fromDay)
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException("to must be on or after from.");
        }

        var days = toDay.DayNumber - fromDay.DayNumber + 1;
        if (days > MaxCustomDays)
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException(
                $"Custom range cannot exceed {MaxCustomDays} days.");
        }

        var from = new DateTimeOffset(fromDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(toDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (to > now.AddDays(1))
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException("to cannot be in the future.");
        }

        var key = $"custom:{fromDay:yyyy-MM-dd}:{toDay:yyyy-MM-dd}";
        return new MonitoringWindow(key, from, to, days <= 1);
    }

    private static bool TryParseDay(string? raw, out DateOnly day) =>
        DateOnly.TryParseExact(raw?.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out day);
}

public static class MonitoringMath
{
    /// <summary>
    /// Percent change against the previous window. Null when the previous
    /// window has no baseline, so the UI does not invent a percentage.
    /// </summary>
    public static double? ChangePercent(long current, long previous)
    {
        if (previous <= 0) return null;
        return Math.Round((current - previous) * 100d / previous, 1);
    }

    public static double? ChangePercent(double? current, double? previous)
    {
        if (current is null || previous is null || previous <= 0) return null;
        return Math.Round((current.Value - previous.Value) * 100d / previous.Value, 1);
    }
}

public sealed record MonitoringCount(long Value, double? ChangePercent);

public sealed record MonitoringUnavailable(string Reason);

/// <summary>
/// One chart bucket. Dau, Wau, and Mau are distinct signed-in players in the
/// 1, 7, and 30 days ending at the bucket end.
/// </summary>
public sealed record MonitoringPoint(
    DateTimeOffset Bucket,
    long Plays,
    long ActiveUsers,
    long NewUsers,
    long Dau,
    long Wau,
    long Mau);

public sealed record MonitoringTopGame(
    string Title,
    string? ThumbnailUrl,
    long Plays,
    double? ChangePercent);

public sealed record MonitoringComponent(string Name, string Status, string? Detail, double? LatencyMs = null);

public sealed record MonitoringProvider(
    string Name,
    string Source,
    string? Status,
    DateTimeOffset? LastSyncedAt);

public sealed record MonitoringOverviewResponse(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    MonitoringCount ActiveUsers,
    MonitoringCount GamePlays,
    MonitoringCount NewUsers,
    MonitoringUnavailable ApiSuccessRate,
    MonitoringUnavailable AverageResponseTime,
    MonitoringUnavailable EstimatedAdRevenue,
    IReadOnlyList<MonitoringPoint> Activity,
    IReadOnlyList<MonitoringTopGame> TopGames,
    IReadOnlyList<MonitoringComponent> Health,
    IReadOnlyList<MonitoringProvider> Providers,
    MonitoringUnavailable RecentErrors,
    MonitoringUnavailable RecentIncidents);

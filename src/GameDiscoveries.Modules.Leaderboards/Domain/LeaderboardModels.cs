namespace GameDiscoveries.Modules.Leaderboards.Domain;

public sealed class LeaderboardDefinition
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Type { get; init; } = string.Empty;
    public string ScoreType { get; init; } = "XP";
    public string PeriodType { get; init; } = string.Empty;
    public string ScopeType { get; init; } = "GLOBAL";
    public string? ScopeValue { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}

public sealed class LeaderboardPeriod
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string Timezone { get; init; } = "Asia/Jakarta";
    public string Status { get; init; } = "SCHEDULED";
    public Guid LeaderboardId { get; init; }
    public bool IsPublic { get; init; } = true;
}

public sealed class LeaderboardEntryRow
{
    public Guid Id { get; init; }
    public Guid LeaderboardId { get; init; }
    public Guid PeriodId { get; init; }
    public Guid UserId { get; init; }
    public long Score { get; init; }
    public int? Rank { get; init; }
    public int? PreviousRank { get; init; }
    public int RankChange { get; init; }
    public int GamesPlayed { get; init; }
    public int ValidSessions { get; init; }
    public long XpEarned { get; init; }
    public DateTimeOffset ScoreReachedAt { get; init; }
    public bool IsDisqualified { get; init; }
    public string? Username { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed record LeaderboardUserDto(Guid Id, string? DisplayName, string? Username, string? AvatarUrl);

public sealed record LeaderboardItemDto(
    int Rank,
    LeaderboardUserDto User,
    long Score,
    int? RankChange,
    string? RankMovement,
    int GamesPlayed,
    int ValidSessions,
    long XpEarned);

public sealed record LeaderboardPeriodDto(Guid Id, string Code, DateTimeOffset StartAt, DateTimeOffset EndAt, string Status, string Timezone);

public sealed record LeaderboardMetaDto(
    string Code,
    string Name,
    string? Description,
    string Type,
    string ScoreType,
    string Version,
    LeaderboardPeriodDto? Period);

public sealed record LeaderboardDetailResponse(
    LeaderboardMetaDto Leaderboard,
    IReadOnlyList<LeaderboardItemDto> Items,
    LeaderboardItemDto? Me,
    int TotalParticipants);

public sealed record UserRankResponse(
    int? Rank,
    long Score,
    int? PreviousRank,
    int? RankChange,
    string? RankMovement,
    double? Percentile,
    int GamesPlayed,
    int ValidSessions,
    long XpEarned,
    int? NextRank,
    long? XpToNextRank,
    LeaderboardPeriodDto? Period);

public sealed record LeaderboardListItemDto(
    string Code,
    string Name,
    string Type,
    string? Description,
    LeaderboardPeriodDto? ActivePeriod,
    int Participants);

public sealed record LeaderboardHistoryItemDto(
    string LeaderboardCode,
    string PeriodCode,
    string PeriodName,
    int? Rank,
    long Score,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public sealed record CompetitionDto(
    string Code,
    string Name,
    string? Description,
    string Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string LeaderboardCode,
    bool RequiresJoin);

public sealed record AdminLeaderboardOverviewDto(
    string Code,
    string Name,
    string? PeriodCode,
    string? PeriodStatus,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    int Participants,
    long TopScore,
    double AverageScore);

public static class LeaderboardRankHelper
{
    public static string? ToMovement(int? previousRank, int? currentRank, int rankChange)
    {
        if (currentRank is null) return null;
        if (previousRank is null) return "NEW";
        if (rankChange > 0) return $"UP_{rankChange}";
        if (rankChange < 0) return $"DOWN_{Math.Abs(rankChange)}";
        return "SAME";
    }

    public static double? Percentile(int? rank, int participants)
    {
        if (rank is null or <= 0 || participants <= 0) return null;
        var value = (1d - ((rank.Value - 1d) / participants)) * 100d;
        return Math.Round(value, 1);
    }
}

public static class LeaderboardPeriodCalculator
{
    public static TimeZoneInfo ResolveTimezone(string timezoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows often uses different IDs; try Jakarta mapping.
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    public static (DateTimeOffset Start, DateTimeOffset End, string Code) CurrentWeekly(DateTimeOffset utcNow, string timezoneId)
    {
        var tz = ResolveTimezone(timezoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var daysFromMonday = ((int)local.DayOfWeek + 6) % 7;
        var localMonday = local.Date.AddDays(-daysFromMonday);
        var startLocal = DateTime.SpecifyKind(localMonday, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(7);
        var start = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
        var end = new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal));
        var week = System.Globalization.ISOWeek.GetWeekOfYear(localMonday);
        var year = System.Globalization.ISOWeek.GetYear(localMonday);
        return (start.ToUniversalTime(), end.ToUniversalTime(), $"WEEKLY_{year}-W{week:D2}");
    }

    public static (DateTimeOffset Start, DateTimeOffset End, string Code) CurrentMonthly(DateTimeOffset utcNow, string timezoneId)
    {
        var tz = ResolveTimezone(timezoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var startLocal = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddMonths(1);
        var start = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
        var end = new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal));
        return (start.ToUniversalTime(), end.ToUniversalTime(), $"MONTHLY_{local.Year}-{local.Month:D2}");
    }

    public static (DateTimeOffset Start, DateTimeOffset End, string Code) AllTime(string timezoneId)
    {
        var start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2099, 12, 31, 23, 59, 59, TimeSpan.Zero);
        return (start, end, "ALL_TIME");
    }
}

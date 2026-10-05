namespace GameDiscoveries.Modules.Streaks.Models;

public sealed record StreakMilestoneInfo(int Days, string Title, int RemainingDays, int? RewardXp);

public sealed record StreakStatusDto(
    int CurrentStreak,
    int LongestStreak,
    string Status,
    DateOnly? StreakStartDate,
    DateOnly? LastQualifyingActivityDate,
    bool TodayQualified,
    int FreezeCount,
    int MaxFreezeCount,
    StreakMilestoneInfo? NextMilestone,
    StreakMilestoneInfo? LastAchievedMilestone);

public sealed record StreakHistoryItemDto(
    Guid Id,
    string EventType,
    int StreakValue,
    DateOnly? ActivityDate,
    int? PreviousStreak,
    int? NewStreak,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed record PagedStreakHistoryDto(
    IReadOnlyList<StreakHistoryItemDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record AdminStreakOverviewDto(
    long UsersWithActiveStreak,
    double AverageCurrentStreak,
    double AverageLongestStreak,
    long UsersAt1Day,
    long UsersAt7Days,
    long UsersAt30Days,
    long FreezesConsumedTotal,
    long MilestonesReached);

public sealed record AdminUserStreakDto(
    Guid UserId,
    StreakStatusDto Streak,
    IReadOnlyList<StreakHistoryItemDto> RecentHistory);

public sealed record AdminFreezeRequest(int Amount, string Reason);

public sealed record AdminStreakResetRequest(string Reason);

public sealed class UserStreakRow
{
    public Guid UserId { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTime? StreakStartDate { get; set; }
    public DateTime? LastQualifyingActivityDate { get; set; }
    public string StreakStatus { get; set; } = "BROKEN";
    public int StreakFreezeCount { get; set; }
}

public sealed class ActivityDayRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime ActivityDate { get; set; }
    public int QualifyingSessionCount { get; set; }
    public bool WasNewDay { get; set; }
}

public sealed class StreakMilestoneEntity
{
    public Guid Id { get; set; }
    public int Days { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? RewardXp { get; set; }
    public bool IsActive { get; set; }
}

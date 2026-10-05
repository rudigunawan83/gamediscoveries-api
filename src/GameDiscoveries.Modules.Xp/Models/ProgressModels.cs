namespace GameDiscoveries.Modules.Xp.Models;

public sealed record LevelInfo(
    int Level,
    string Title,
    string? Description,
    long TotalXp,
    long CurrentLevelXp,
    long NextLevelXp,
    double ProgressPercentage,
    bool IsMaxLevel,
    int? NextLevel = null,
    string? NextTitle = null);

public sealed record LevelDefinitionDto(
    int Level,
    long RequiredTotalXp,
    string Title,
    string? Description,
    bool IsActive);

public sealed record UserProgressResponse(
    ProgressUserDto User,
    LevelInfo Level,
    ProgressStatsDto Stats,
    ProgressStreakDto? Streak = null);

public sealed record ProgressStreakDto(
    int Current,
    int Longest,
    string Status,
    bool TodayQualified,
    int FreezeCount,
    int? NextMilestone);

public sealed record ProgressUserDto(
    Guid Id,
    string Name,
    string? AvatarUrl);

public sealed record ProgressStatsDto(
    int TotalGameSessions,
    int UniqueGamesPlayed,
    int Favorites,
    int CurrentStreak,
    int LongestStreak);

public sealed record PagedXpTransactionsResponse(
    IReadOnlyList<XpTransactionDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record GamificationOverviewResponse(
    long TotalUsers,
    long UsersWithXp,
    long TotalXpAwarded,
    long XpToday,
    long XpThisWeek,
    double AverageLevel,
    IReadOnlyList<AnalyticsNamedCountLite> LevelDistribution,
    IReadOnlyList<AnalyticsNamedCountLite> XpByRule);

public sealed record AnalyticsNamedCountLite(string Name, long Count);

public sealed record AdminUserListItem(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    int Level,
    long TotalXp,
    int UniqueGamesPlayed,
    int Favorites,
    int CurrentStreak,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset CreatedAt);

public sealed record AdminUserDetailResponse(
    AdminUserListItem User,
    LevelInfo Level,
    ProgressStatsDto Stats,
    IReadOnlyList<XpTransactionDto> RecentTransactions);

public sealed record UpsertLevelRequest(
    int Level,
    long RequiredTotalXp,
    string Title,
    string? Description,
    bool IsActive = true);

public sealed record AuditLogDto(
    Guid Id,
    Guid? AdminId,
    string Action,
    string TargetType,
    string TargetId,
    string? Reason,
    DateTimeOffset CreatedAt);

// Extended award result for level-up UX
public sealed record XpAwardResultV2(
    bool Success,
    int XpAwarded,
    long TotalXp,
    string? Reason,
    IReadOnlyList<XpAwardedItem> Transactions,
    int? PreviousLevel,
    int? CurrentLevel,
    bool LeveledUp,
    LevelInfo? Level);

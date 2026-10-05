namespace GameDiscoveries.Modules.Achievements.Models;

public sealed record AchievementDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    string Category,
    string Difficulty,
    string? Icon,
    bool IsSecret,
    bool IsUnlocked,
    DateTimeOffset? UnlockedAt,
    int ProgressValue,
    int TargetValue,
    double ProgressPercentage,
    int RewardXp);

public sealed record AchievementListResponse(
    IReadOnlyList<AchievementDto> Items,
    AchievementOverviewStats Overview);

public sealed record AchievementHistoryDto(
    Guid Id,
    Guid UserId,
    Guid AchievementDefinitionId,
    string EventType,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed record AdminAchievementDefinitionDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    string Category,
    string Difficulty,
    string RequirementType,
    int TargetValue,
    int RewardXp,
    string? Icon,
    bool IsSecret,
    bool IsActive,
    Guid? SeasonId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long UnlockedCount);

public sealed record AchievementOverviewStats(
    int TotalDefinitions,
    int ActiveDefinitions,
    int UserUnlocked,
    int UserInProgress,
    double CompletionPercentage);

public sealed record AdminAchievementOverviewStats(
    long TotalDefinitions,
    long ActiveDefinitions,
    long SecretDefinitions,
    long TotalUnlocks,
    long UnlocksToday);

public sealed record AdminAchievementUserDto(
    Guid UserId,
    DateTimeOffset UnlockedAt,
    int ProgressValue,
    int TargetValue,
    Guid? GrantedByAdminId,
    DateTimeOffset? RevokedAt);

public sealed record UpsertAchievementRequest(
    string Code,
    string Title,
    string Description,
    string Category,
    string Difficulty,
    string RequirementType,
    int TargetValue,
    int RewardXp,
    string? Icon,
    bool IsSecret,
    bool IsActive,
    Guid? SeasonId);

public sealed record AdminAchievementActionRequest(string? Reason);

public sealed record AchievementDefinitionEntity
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string RequirementType { get; init; } = string.Empty;
    public int TargetValue { get; init; }
    public int RewardXp { get; init; }
    public string? Icon { get; init; }
    public bool IsSecret { get; init; }
    public bool IsActive { get; init; }
    public Guid? SeasonId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public long UnlockedCount { get; init; }
}

public sealed record AchievementUnlockEntity
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid AchievementDefinitionId { get; init; }
    public DateTimeOffset UnlockedAt { get; init; }
    public int ProgressValue { get; init; }
    public int TargetValue { get; init; }
    public Guid? RewardTransactionId { get; init; }
    public bool IsNotified { get; init; }
    public Guid? GrantedByAdminId { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public Guid? RevokedByAdminId { get; init; }
    public string? RevokeReason { get; init; }
}

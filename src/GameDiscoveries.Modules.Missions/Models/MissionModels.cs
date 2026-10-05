namespace GameDiscoveries.Modules.Missions.Models;

public sealed class MissionTemplateEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string RequirementType { get; set; } = string.Empty;
    public int TargetValue { get; set; }
    public int RewardXp { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UserMissionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MissionTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public int ProgressValue { get; set; }
    public int TargetValue { get; set; }
    public int RewardXp { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? RewardTransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record MissionPeriod(DateTimeOffset Start, DateTimeOffset End);

public sealed record MissionDto(
    Guid Id,
    string Code,
    string Type,
    string Title,
    string Description,
    string? Icon,
    string RequirementType,
    int Progress,
    int Target,
    double Percentage,
    int RewardXp,
    string Status,
    string Difficulty,
    DateTimeOffset PeriodStart,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? CompletedAt);

public sealed record MyMissionsResponse(
    IReadOnlyList<MissionDto> Daily,
    IReadOnlyList<MissionDto> Weekly,
    DateTimeOffset DailyExpiresAt,
    DateTimeOffset WeeklyExpiresAt,
    string TimeZone);

public sealed record PagedMissionsResponse(
    IReadOnlyList<MissionDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record UpsertMissionTemplateRequest(
    string Code,
    string Type,
    string Title,
    string Description,
    string RequirementType,
    int TargetValue,
    int RewardXp,
    string Difficulty,
    string? Icon = null,
    int SortOrder = 0,
    bool IsActive = true);

public sealed record MissionTemplateDto(
    Guid Id,
    string Code,
    string Type,
    string Title,
    string Description,
    string? Icon,
    string RequirementType,
    int TargetValue,
    int RewardXp,
    string Difficulty,
    bool IsActive,
    int SortOrder,
    DateTimeOffset UpdatedAt);

public sealed record MissionAnalyticsResponse(
    long Assigned,
    long Completed,
    long Expired,
    double CompletionRate,
    double ExpirationRate,
    double AverageProgress,
    long XpAwarded,
    IReadOnlyList<NamedCountDto> CompletionByCode);

public sealed record NamedCountDto(string Name, long Count);

public sealed record MissionCompletionResult(
    bool MissionCompleted,
    MissionDto? Mission,
    int XpAwarded,
    bool LeveledUp,
    int? NewLevel);

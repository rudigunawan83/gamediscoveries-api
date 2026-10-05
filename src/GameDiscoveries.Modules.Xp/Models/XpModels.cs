namespace GameDiscoveries.Modules.Xp.Models;

public sealed record XpAwardRequest(
    Guid UserId,
    string RuleCode,
    string EventType,
    string ReferenceType,
    string ReferenceId,
    int XpAmount,
    string Description,
    Dictionary<string, object?>? Metadata = null,
    Guid? AdminId = null,
    Guid? ReversalOfTransactionId = null);

public sealed record XpAwardedItem(string RuleCode, int Xp);

public sealed record XpAwardResult(
    bool Success,
    int XpAwarded,
    long TotalXp,
    string? Reason,
    IReadOnlyList<XpAwardedItem> Transactions,
    int? PreviousLevel = null,
    int? CurrentLevel = null,
    bool LeveledUp = false,
    LevelInfo? Level = null);

public sealed record UserXpSummary(
    long TotalXp,
    int Level,
    long CurrentLevelXp,
    IReadOnlyList<XpTransactionDto> RecentTransactions);

public sealed record XpTransactionDto(
    Guid TransactionId,
    string RuleCode,
    string EventType,
    string ReferenceType,
    string ReferenceId,
    int XpAmount,
    string Description,
    DateTimeOffset CreatedAt);

public sealed class AdminXpAdjustmentRequest
{
    public int? Amount { get; init; }
    public int? XpAmount { get; init; }
    public string? Reason { get; init; }
    public string? Description { get; init; }

    public int ResolvedAmount => Amount ?? XpAmount ?? 0;
    public string ResolvedReason => (Reason ?? Description ?? string.Empty).Trim();
}

public sealed record ReverseXpRequest(string? Reason);

public sealed record AdminReasonRequest(string? Reason);

public sealed record AdminSuspendRequest(string? Reason);

public sealed class XpTransactionEntity
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public int XpAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public Guid? AdminId { get; set; }
    public Guid? ReversalOfTransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UserProgressEntity
{
    public Guid UserId { get; set; }
    public long TotalXp { get; set; }
    public int Level { get; set; } = 1;
    public long CurrentLevelXp { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int GamesPlayed { get; set; }
    public int FavoritesCount { get; set; }
    public int UniqueGamesPlayed { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
}

namespace GameDiscoveries.BuildingBlocks.Abstractions;

/// <summary>
/// Fan-out after XP ledger writes. Implemented by Modules.Leaderboards.
/// Must never mutate XP — only project eligible amounts into leaderboards.
/// </summary>
public interface ILeaderboardXpSink
{
    Task OnXpTransactionAsync(
        Guid userId,
        string ruleCode,
        string eventType,
        int xpAmount,
        DateTimeOffset createdAt,
        string referenceType,
        string referenceId,
        CancellationToken cancellationToken = default);
}

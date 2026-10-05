namespace GameDiscoveries.BuildingBlocks.Abstractions;

/// <summary>
/// Fan-out hooks for Achievement evaluation. Implemented by Modules.Achievements.
/// Callers must not unlock achievements client-side — only trigger evaluation.
/// </summary>
public interface IAchievementActivitySink
{
    Task OnValidSessionEndedAsync(
        Guid userId,
        Guid gameId,
        string sessionId,
        int activeSeconds,
        CancellationToken cancellationToken = default);

    Task OnFavoriteAddedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task OnRatingCreatedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task OnReviewCreatedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task OnLevelUpAsync(
        Guid userId,
        int newLevel,
        CancellationToken cancellationToken = default);

    Task OnMissionCompletedAsync(
        Guid userId,
        string missionType,
        CancellationToken cancellationToken = default);

    Task OnStreakProgressAsync(
        Guid userId,
        int currentStreak,
        int longestStreak,
        CancellationToken cancellationToken = default);
}

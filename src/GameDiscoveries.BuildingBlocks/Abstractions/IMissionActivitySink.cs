namespace GameDiscoveries.BuildingBlocks.Abstractions;

/// <summary>
/// Optional sinks for gamification activity (missions, future streaks).
/// Implemented by Modules.Missions; called from Favorites / session handlers.
/// </summary>
public interface IMissionActivitySink
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
}

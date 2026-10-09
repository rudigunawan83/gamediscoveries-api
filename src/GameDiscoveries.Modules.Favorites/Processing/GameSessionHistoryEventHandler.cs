using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Favorites.Data;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Favorites.Processing;

/// <summary>
/// Consumes GAME_SESSION_END and adds the session's server-measured active time to the
/// player's history, so "continue playing" is the same on web, Android and iOS.
/// Event metadata is ignored; the session row is the source of truth.
/// </summary>
public sealed class GameSessionHistoryEventHandler(
    IUserLibraryRepository repository,
    ILogger<GameSessionHistoryEventHandler> logger) : IAnalyticsEventHandler
{
    public string Name => "Favorites.GameSessionHistory";

    public async Task HandleAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(command.EventType, AnalyticsEventTypes.GameSessionEnd, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (command.UserId is not { } userId || string.IsNullOrWhiteSpace(command.SessionId))
        {
            return;
        }

        try
        {
            var recorded = await repository.RecordEndedSessionAsync(userId, command.SessionId, cancellationToken);
            if (recorded)
            {
                logger.LogInformation("play_history_recorded sessionId={SessionId}", command.SessionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Play history update failed for session {SessionId}", command.SessionId);
        }
    }
}

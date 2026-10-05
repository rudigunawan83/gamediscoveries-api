using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Processing;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Missions.Processing;

/// <summary>
/// Consumes GAME_SESSION_END and refreshes mission progress for authenticated users.
/// Runs alongside the XP session handler; does not replace it.
/// </summary>
public sealed class GameSessionMissionEventHandler(
    IEnumerable<IMissionActivitySink> sinks,
    IEnumerable<IAchievementActivitySink> achievementSinks,
    ILogger<GameSessionMissionEventHandler> logger) : IAnalyticsEventHandler
{
    public string Name => "Missions.GameSessionEnd";

    public async Task HandleAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(command.EventType, AnalyticsEventTypes.GameSessionEnd, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (command.UserId is null || command.GameId is null || string.IsNullOrWhiteSpace(command.SessionId))
        {
            return;
        }

        var activeSeconds = 0;
        if (command.Metadata.TryGetValue("activeSeconds", out var raw) && raw is not null)
        {
            _ = int.TryParse(raw.ToString(), out activeSeconds);
        }

        try
        {
            foreach (var sink in sinks)
            {
                await sink.OnValidSessionEndedAsync(
                    command.UserId.Value,
                    command.GameId.Value,
                    command.SessionId,
                    activeSeconds,
                    cancellationToken);
            }

            foreach (var sink in achievementSinks)
            {
                await sink.OnValidSessionEndedAsync(
                    command.UserId.Value,
                    command.GameId.Value,
                    command.SessionId,
                    activeSeconds,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mission progress failed for session {SessionId}", command.SessionId);
        }
    }
}

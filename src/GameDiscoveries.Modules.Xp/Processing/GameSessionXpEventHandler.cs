using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Xp.Processing;

/// <summary>
/// Consumes GAME_SESSION_END analytics events and awards XP for authenticated users.
/// </summary>
public sealed class GameSessionXpEventHandler(
    IXpEngine xpEngine,
    ILogger<GameSessionXpEventHandler> logger) : IAnalyticsEventHandler
{
    public string Name => "Xp.GameSessionEnd";

    public async Task HandleAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(command.EventType, AnalyticsEventTypes.GameSessionEnd, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (command.UserId is null || string.IsNullOrWhiteSpace(command.SessionId))
        {
            return;
        }

        try
        {
            var result = await xpEngine.ProcessGameSessionEndAsync(command.SessionId, cancellationToken);
            logger.LogInformation(
                "xp_rule_evaluated sessionId={SessionId} xpAwarded={Xp} reason={Reason}",
                command.SessionId,
                result.XpAwarded,
                result.Reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "XP processing failed for session {SessionId}", command.SessionId);
        }
    }
}

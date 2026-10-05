using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Processing;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Achievements.Processing;

public sealed class AchievementAnalyticsEventHandler(
    IEnumerable<IAchievementActivitySink> sinks,
    ILogger<AchievementAnalyticsEventHandler> logger) : IAnalyticsEventHandler
{
    public string Name => "Achievements.Analytics";

    public async Task HandleAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
    {
        if (command.UserId is null)
        {
            return;
        }

        try
        {
            if (string.Equals(command.EventType, AnalyticsEventTypes.LevelUp, StringComparison.OrdinalIgnoreCase))
            {
                var newLevel = 0;
                if (command.Metadata.TryGetValue("newLevel", out var raw) && raw is not null)
                {
                    _ = int.TryParse(raw.ToString(), out newLevel);
                }

                foreach (var sink in sinks)
                {
                    await sink.OnLevelUpAsync(command.UserId.Value, newLevel, cancellationToken);
                }
            }
            else if (string.Equals(command.EventType, AnalyticsEventTypes.DailyMissionCompleted, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(command.EventType, AnalyticsEventTypes.WeeklyChallengeCompleted, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var sink in sinks)
                {
                    await sink.OnMissionCompletedAsync(command.UserId.Value, command.EventType, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement analytics handling failed for {EventType}", command.EventType);
        }
    }
}

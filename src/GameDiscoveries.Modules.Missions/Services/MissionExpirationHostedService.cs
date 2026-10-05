using GameDiscoveries.Modules.Missions.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Missions.Services;

/// <summary>
/// Periodically expires past ACTIVE missions. Assignment remains lazy on API/activity.
/// </summary>
public sealed class MissionExpirationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<MissionExpirationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IMissionStore>();
                await store.ExpireAllPastMissionsAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Mission expiration sweep failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

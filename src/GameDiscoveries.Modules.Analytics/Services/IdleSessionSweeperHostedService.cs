using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Analytics.Services;

/// <summary>
/// Periodically ends game sessions abandoned without an end call.
/// </summary>
public sealed class IdleSessionSweeperHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<IdleSessionSweeperHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sessions = scope.ServiceProvider.GetRequiredService<IGamePlaySessionService>();
                var finalized = await sessions.FinalizeIdleSessionsAsync(stoppingToken);
                if (finalized > 0)
                {
                    logger.LogInformation("idle_sessions_finalized count={Count}", finalized);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Idle game session sweep failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

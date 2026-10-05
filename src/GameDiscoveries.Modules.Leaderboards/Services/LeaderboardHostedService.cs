using GameDiscoveries.Modules.Leaderboards.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Leaderboards.Services;

public sealed class LeaderboardHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<LeaderboardOptions> options,
    ILogger<LeaderboardHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(50), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var lastReconcile = DateTimeOffset.MinValue;
        var lastSnapshot = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Value.Enabled)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();
                    await service.EnsureActivePeriodsAsync(stoppingToken);

                    var now = DateTimeOffset.UtcNow;
                    if (now - lastSnapshot >= TimeSpan.FromMinutes(Math.Clamp(options.Value.SnapshotIntervalMinutes, 5, 60)))
                    {
                        await service.SnapshotActiveAsync(stoppingToken);
                        lastSnapshot = now;
                    }

                    if (now - lastReconcile >= TimeSpan.FromMinutes(Math.Clamp(options.Value.ReconciliationIntervalMinutes, 15, 360)))
                    {
                        await service.ReconcileActiveAsync(stoppingToken);
                        lastReconcile = now;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "LeaderboardHostedServiceFailed");
                }
            }

            var minutes = Math.Clamp(options.Value.PeriodRotationIntervalMinutes, 1, 60);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

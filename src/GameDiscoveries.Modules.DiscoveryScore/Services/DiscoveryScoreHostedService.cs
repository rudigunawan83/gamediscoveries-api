using GameDiscoveries.Modules.DiscoveryScore.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.DiscoveryScore.Services;

public sealed class DiscoveryScoreHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DiscoveryScoreOptions> options,
    ILogger<DiscoveryScoreHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay so migrations/app warm-up finish
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Value.Enabled)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var aggregation = scope.ServiceProvider.GetRequiredService<IMetricAggregationService>();
                    var scores = scope.ServiceProvider.GetRequiredService<IDiscoveryScoreService>();
                    var trending = scope.ServiceProvider.GetRequiredService<ITrendingService>();

                    await aggregation.AggregateAsync(stoppingToken);
                    await scores.RecalculateAllAsync(stoppingToken);
                    await trending.CalculateSnapshotsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "DiscoveryScoreCalculationFailed");
                }
            }

            var minutes = Math.Clamp(options.Value.AggregationIntervalMinutes, 5, 360);
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

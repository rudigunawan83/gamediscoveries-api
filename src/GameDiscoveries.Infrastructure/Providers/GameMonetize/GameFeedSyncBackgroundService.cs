using GameDiscoveries.BuildingBlocks.Configuration;
using GameDiscoveries.BuildingBlocks.Feeds;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public sealed class GameFeedSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<GameFeedSyncOptions> options,
    ILogger<GameFeedSyncBackgroundService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DateTimeOffset _nextLatest = DateTimeOffset.MinValue;
    private DateTimeOffset _nextPopular = DateTimeOffset.MinValue;
    private DateTimeOffset _nextCategory = DateTimeOffset.MinValue;
    private DateTimeOffset _nextMobile = DateTimeOffset.MinValue;
    private DateTimeOffset _nextTwoPlayer = DateTimeOffset.MinValue;
    private DateTimeOffset _nextFeatured = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sync = options.Value;
        if (!sync.Enabled)
        {
            logger.LogInformation("[GameFeedSync] Background synchronization is disabled");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Clamp(sync.StartupDelaySeconds, 0, 300));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        logger.LogInformation("[GameFeedSync] Background synchronization started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            await TryRunAsync(GameFeedType.Latest, now >= _nextLatest, () =>
            {
                _nextLatest = now.AddMinutes(Math.Max(1, sync.LatestIntervalMinutes));
            }, stoppingToken);

            await TryRunAsync(GameFeedType.Popular, now >= _nextPopular, () =>
            {
                _nextPopular = now.AddMinutes(Math.Max(1, sync.PopularIntervalMinutes));
            }, stoppingToken);

            if (now >= _nextCategory)
            {
                _nextCategory = now.AddMinutes(Math.Max(1, sync.CategoryIntervalMinutes));
                foreach (var categoryFeed in new[]
                         {
                             GameFeedType.Action,
                             GameFeedType.Puzzle,
                             GameFeedType.Racing,
                             GameFeedType.Sports,
                             GameFeedType.Multiplayer
                         })
                {
                    await TryRunAsync(categoryFeed, due: true, markNext: static () => { }, stoppingToken);
                }
            }

            await TryRunAsync(GameFeedType.Mobile, now >= _nextMobile, () =>
            {
                _nextMobile = now.AddMinutes(Math.Max(1, sync.MobileIntervalMinutes));
            }, stoppingToken);

            await TryRunAsync(GameFeedType.TwoPlayer, now >= _nextTwoPlayer, () =>
            {
                _nextTwoPlayer = now.AddMinutes(Math.Max(1, sync.TwoPlayerIntervalMinutes));
            }, stoppingToken);

            await TryRunAsync(GameFeedType.Featured, now >= _nextFeatured, () =>
            {
                _nextFeatured = now.AddMinutes(Math.Max(1, sync.FeaturedIntervalMinutes));
            }, stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task TryRunAsync(
        GameFeedType feedType,
        bool due,
        Action markNext,
        CancellationToken cancellationToken)
    {
        if (!due)
        {
            return;
        }

        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            logger.LogDebug("[GameFeedSync] Skipping {FeedType}; another sync is in progress", feedType);
            return;
        }

        try
        {
            markNext();
            logger.LogInformation("[GameFeedSync] Starting {FeedType} feed", feedType);

            await using var scope = scopeFactory.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<IGameFeedImportService>();
            await importer.ImportAsync(feedType, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[GameFeedSync] Unhandled failure for {FeedType}", feedType);
        }
        finally
        {
            _gate.Release();
        }
    }

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }
}

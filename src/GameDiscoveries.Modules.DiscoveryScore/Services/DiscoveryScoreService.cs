using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.DiscoveryScore.Data;
using GameDiscoveries.Modules.DiscoveryScore.Domain;
using GameDiscoveries.Modules.DiscoveryScore.Models;
using GameDiscoveries.Modules.DiscoveryScore.Options;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.DiscoveryScore.Services;

public interface IDiscoveryScoreService
{
    Task RecalculateAllAsync(CancellationToken cancellationToken = default);

    Task<DiscoveryScoreExplainDto?> GetExplainAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task<AdminDiscoveryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<AdminDiscoveryConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);

    Task<AdminDiscoveryConfigDto> UpdateConfigAsync(
        Guid adminId,
        UpsertDiscoveryConfigRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITrendingService
{
    Task CalculateSnapshotsAsync(CancellationToken cancellationToken = default);

    Task<DiscoveryRankingResponse> GetRankingAsync(
        string type,
        string period,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public interface IMetricAggregationService
{
    Task AggregateAsync(CancellationToken cancellationToken = default);
}

public sealed class MetricAggregationService(
    IDiscoveryScoreStore store,
    ILogger<MetricAggregationService> logger) : IMetricAggregationService
{
    public async Task AggregateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var signals = await store.LoadSignalsAsync(now, cancellationToken);
        var dayStart = now.AddHours(-24);
        var hourStart = now.AddHours(-1);
        var weekStart = now.AddDays(-7);

        foreach (var signal in signals)
        {
            await store.UpsertMetricAsync(
                signal.GameId, dayStart, now, DiscoveryWindowTypes.Daily, signal, true, cancellationToken);
            await store.UpsertMetricAsync(
                signal.GameId, hourStart, now, DiscoveryWindowTypes.Hourly, signal, true, cancellationToken);
            await store.UpsertMetricAsync(
                signal.GameId, weekStart, now, DiscoveryWindowTypes.Weekly, signal, true, cancellationToken);
        }

        logger.LogInformation(
            "MetricAggregationCompleted games={Count} at={At}",
            signals.Count,
            now);
    }
}

public sealed class DiscoveryScoreService(
    IDiscoveryScoreStore store,
    IOptions<DiscoveryScoreOptions> options,
    IAuditLogService audit,
    ILogger<DiscoveryScoreService> logger) : IDiscoveryScoreService
{
    public async Task RecalculateAllAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var started = DateTimeOffset.UtcNow;
        logger.LogInformation("DiscoveryScoreCalculationStarted at={At}", started);

        var config = await store.GetConfigAsync(cancellationToken);
        var signals = await store.LoadSignalsAsync(started, cancellationToken);
        if (signals.Count == 0)
        {
            logger.LogInformation("DiscoveryScoreCalculationCompleted games=0");
            return;
        }

        var globalAvg = signals.Where(s => s.RatingsLifetime > 0).Select(s => s.AvgRating).DefaultIfEmpty(3.5).Average();
        var views = signals.Select(s => (double)s.Views24h).ToList();
        var starts = signals.Select(s => (double)s.Starts24h).ToList();
        var valid = signals.Select(s => (double)s.ValidSessions24h).ToList();
        var unique = signals.Select(s => (double)s.UniqueUsers24h).ToList();
        var active = signals.Select(s => (double)s.ActiveSeconds24h).ToList();
        var returning = signals.Select(s => (double)s.ReturningUsers24h).ToList();
        var avgSession = signals.Select(s => s.ValidSessions24h > 0 ? (double)s.ActiveSeconds24h / s.ValidSessions24h : 0d).ToList();
        var sessionsPerUser = signals.Select(s => s.UniqueUsers24h > 0 ? (double)s.Sessions24h / s.UniqueUsers24h : 0d).ToList();
        var validRate = signals.Select(s => s.Sessions24h > 0 ? (double)s.ValidSessions24h / s.Sessions24h : 0d).ToList();
        var favRate = signals.Select(s => s.UniqueUsers24h > 0 ? (double)s.Favorites24h / s.UniqueUsers24h : 0d).ToList();
        var reviewRate = signals.Select(s => s.ValidSessionsLifetime > 0 ? (double)s.ReviewsLifetime / s.ValidSessionsLifetime : 0d).ToList();

        foreach (var signal in signals)
        {
            var popularity = DiscoveryScoreFormulas.PopularityScore(
                MetricNormalization.NormalizeLog1pMinMax(views, signal.Views24h),
                MetricNormalization.NormalizeLog1pMinMax(starts, signal.Starts24h),
                MetricNormalization.NormalizeLog1pMinMax(valid, signal.ValidSessions24h),
                MetricNormalization.NormalizeLog1pMinMax(unique, signal.UniqueUsers24h));

            var avgS = signal.ValidSessions24h > 0 ? (double)signal.ActiveSeconds24h / signal.ValidSessions24h : 0;
            var spu = signal.UniqueUsers24h > 0 ? (double)signal.Sessions24h / signal.UniqueUsers24h : 0;
            var vr = signal.Sessions24h > 0 ? (double)signal.ValidSessions24h / signal.Sessions24h : 0;
            var fr = signal.UniqueUsers24h > 0 ? (double)signal.Favorites24h / signal.UniqueUsers24h : 0;
            var rr = signal.ValidSessionsLifetime > 0 ? (double)signal.ReviewsLifetime / signal.ValidSessionsLifetime : 0;

            var engagement = DiscoveryScoreFormulas.EngagementScore(
                MetricNormalization.NormalizeLog1pMinMax(avgSession, avgS),
                MetricNormalization.NormalizeLog1pMinMax(active, signal.ActiveSeconds24h),
                MetricNormalization.NormalizeLog1pMinMax(returning, signal.ReturningUsers24h),
                MetricNormalization.NormalizeLog1pMinMax(sessionsPerUser, spu),
                MetricNormalization.NormalizePercentile(validRate, vr));

            var bayesian = DiscoveryScoreFormulas.BayesianRating(
                signal.AvgRating <= 0 ? globalAvg : signal.AvgRating,
                signal.RatingsLifetime,
                config.BayesianM,
                globalAvg);

            var quality = DiscoveryScoreFormulas.QualityScore(
                bayesian,
                MetricNormalization.NormalizePercentile(favRate, fr),
                MetricNormalization.NormalizePercentile(reviewRate, rr));

            var momentum = MetricNormalization.NormalizeGrowth(
                signal.ValidSessions24h, signal.ValidSessionsPrev24h, config.GrowthSmoothing);

            var growth = DiscoveryScoreFormulas.GrowthScore(
                MetricNormalization.NormalizeGrowth(signal.Views24h, signal.ViewsPrev24h, config.GrowthSmoothing),
                MetricNormalization.NormalizeGrowth(signal.Starts24h, signal.StartsPrev24h, config.GrowthSmoothing),
                MetricNormalization.NormalizeGrowth(signal.UniqueUsers24h, signal.UniqueUsersPrev24h, config.GrowthSmoothing),
                MetricNormalization.NormalizeGrowth(signal.ValidSessions24h, signal.ValidSessionsPrev24h, config.GrowthSmoothing),
                MetricNormalization.NormalizeGrowth(signal.Favorites24h, signal.FavoritesPrev24h, config.GrowthSmoothing));

            var published = signal.PublishedAt ?? signal.CreatedAt;
            var ageDays = Math.Max(0, (started - published).TotalDays);
            var freshness = DiscoveryScoreFormulas.FreshnessScore(ageDays, config.FreshnessDecayDays);

            // Cold start: baseline from freshness when almost no activity
            if (signal.ValidSessionsLifetime == 0 && signal.Views24h == 0)
            {
                popularity = Math.Max(popularity, freshness * 0.35);
                engagement = Math.Max(engagement, freshness * 0.25);
                quality = Math.Max(quality, 40);
            }

            var discovery = DiscoveryScoreFormulas.DiscoveryScore(
                popularity, engagement, quality, momentum, growth, freshness,
                config.PopularityWeight, config.EngagementWeight, config.QualityWeight,
                config.MomentumWeight, config.GrowthWeight, config.FreshnessWeight);

            var recentActivity = MetricNormalization.Clamp(
                0.4 * MetricNormalization.NormalizeLog1pMinMax(valid, signal.ValidSessions24h) +
                0.3 * MetricNormalization.NormalizeLog1pMinMax(unique, signal.UniqueUsers24h) +
                0.3 * MetricNormalization.NormalizeLog1pMinMax(views, signal.Views24h));

            var trending = DiscoveryScoreFormulas.TrendingScore(
                recentActivity, momentum, growth, engagement, freshness,
                config.TrendingRecentWeight, config.TrendingMomentumWeight, config.TrendingGrowthWeight,
                config.TrendingEngagementWeight, config.TrendingFreshnessWeight);

            var growthPct = (((signal.ValidSessions24h + config.GrowthSmoothing) /
                              (signal.ValidSessionsPrev24h + config.GrowthSmoothing)) - 1d) * 100d;
            var isNew = ageDays <= config.NewGameDays;
            var trendState = DiscoveryScoreFormulas.ResolveTrendState(
                growthPct, trending, isNew, config.RisingGrowthThreshold, config.DecliningGrowthThreshold);

            var entity = new DiscoveryScoreEntity
            {
                Id = Guid.NewGuid(),
                GameId = signal.GameId,
                Score = Math.Round(discovery, 2),
                PopularityScore = Math.Round(popularity, 2),
                EngagementScore = Math.Round(engagement, 2),
                QualityScore = Math.Round(quality, 2),
                MomentumScore = Math.Round(momentum, 2),
                GrowthScore = Math.Round(growth, 2),
                FreshnessScore = Math.Round(freshness, 2),
                TrendingScore = Math.Round(trending, 2),
                TrendState = trendState,
                TrendPercentage = Math.Round(growthPct, 2),
                ScoreVersion = config.ScoreVersion,
                CalculatedAt = started,
                ValidUntil = started.AddMinutes(config.ScoreValidMinutes)
            };

            await store.UpsertScoreAsync(entity, cancellationToken);
            await store.InsertScoreHistoryAsync(entity, cancellationToken);
        }

        await store.CleanupRetentionAsync(
            options.Value.RetentionHourlyDays,
            options.Value.RetentionDailyDays,
            cancellationToken);

        logger.LogInformation(
            "DiscoveryScoreCalculationCompleted games={Count} durationMs={Ms} version={Version}",
            signals.Count,
            (DateTimeOffset.UtcNow - started).TotalMilliseconds,
            config.ScoreVersion);
    }

    public async Task<DiscoveryScoreExplainDto?> GetExplainAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var score = await store.GetScoreAsync(gameId, cancellationToken);
        if (score is null)
        {
            return null;
        }

        var factors = new List<DiscoveryScoreFactorDto>
        {
            new("Popularity", score.PopularityScore, DiscoveryScoreFormulas.ImpactLabel(score.PopularityScore)),
            new("Engagement", score.EngagementScore, DiscoveryScoreFormulas.ImpactLabel(score.EngagementScore)),
            new("Quality", score.QualityScore, DiscoveryScoreFormulas.ImpactLabel(score.QualityScore)),
            new("Momentum", score.MomentumScore, DiscoveryScoreFormulas.ImpactLabel(score.MomentumScore)),
            new("Growth", score.GrowthScore, DiscoveryScoreFormulas.ImpactLabel(score.GrowthScore)),
            new("Freshness", score.FreshnessScore, DiscoveryScoreFormulas.ImpactLabel(score.FreshnessScore))
        }.OrderByDescending(f => f.Score).ToList();

        return new DiscoveryScoreExplainDto(
            score.GameId,
            score.Score,
            score.TrendingScore,
            score.TrendState,
            score.TrendPercentage,
            score.ScoreVersion,
            score.CalculatedAt,
            factors);
    }

    public Task<AdminDiscoveryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        store.GetOverviewAsync(cancellationToken);

    public async Task<AdminDiscoveryConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await store.GetConfigAsync(cancellationToken);
        return ToConfigDto(config);
    }

    public async Task<AdminDiscoveryConfigDto> UpdateConfigAsync(
        Guid adminId,
        UpsertDiscoveryConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!DiscoveryScoreFormulas.ValidateWeights(
                request.PopularityWeight, request.EngagementWeight, request.QualityWeight,
                request.MomentumWeight, request.GrowthWeight, request.FreshnessWeight))
        {
            throw new ValidationException("Discovery score weights must sum to 1.0.");
        }

        if (!DiscoveryScoreFormulas.ValidateWeights(
                request.TrendingRecentWeight, request.TrendingMomentumWeight, request.TrendingGrowthWeight,
                request.TrendingEngagementWeight, request.TrendingFreshnessWeight))
        {
            throw new ValidationException("Trending weights must sum to 1.0.");
        }

        var before = await store.GetConfigAsync(cancellationToken);
        var updated = await store.UpdateConfigAsync(request, adminId, before.ScoreVersion + 1, cancellationToken);
        await audit.WriteAsync(
            adminId,
            AuditActions.AdminDiscoveryConfigUpdated,
            "DiscoveryConfig",
            updated.Id.ToString("D"),
            before,
            updated,
            request.Reason,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "DiscoveryConfigurationUpdated adminId={AdminId} version={Version}",
            adminId,
            updated.ScoreVersion);

        return ToConfigDto(updated);
    }

    private static AdminDiscoveryConfigDto ToConfigDto(DiscoveryConfigEntity c) =>
        new(
            c.ScoreVersion,
            c.PopularityWeight, c.EngagementWeight, c.QualityWeight,
            c.MomentumWeight, c.GrowthWeight, c.FreshnessWeight,
            c.TrendingRecentWeight, c.TrendingMomentumWeight, c.TrendingGrowthWeight,
            c.TrendingEngagementWeight, c.TrendingFreshnessWeight,
            c.FreshnessDecayDays, c.NewGameDays, c.MinValidSessionsForNewTrending,
            c.GrowthSmoothing, c.BayesianM, c.RisingGrowthThreshold, c.DecliningGrowthThreshold,
            c.ScoreValidMinutes, c.UpdatedAt);
}

public sealed class TrendingService(
    IDiscoveryScoreStore store,
    ICacheService cache,
    IOptions<DiscoveryScoreOptions> options,
    ILogger<TrendingService> logger) : ITrendingService, IDiscoveryRankingProvider
{
    public async Task CalculateSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var config = await store.GetConfigAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var signals = (await store.LoadSignalsAsync(now, cancellationToken)).ToDictionary(s => s.GameId);
        var scores = new List<DiscoveryScoreEntity>();

        foreach (var signal in signals.Values)
        {
            var score = await store.GetScoreAsync(signal.GameId, cancellationToken);
            if (score is not null)
            {
                scores.Add(score);
            }
        }

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.Trending,
            DiscoveryPeriodTypes.Day,
            scores.OrderByDescending(s => s.TrendingScore).ThenByDescending(s => s.Score).ThenBy(s => s.GameId),
            s => s.TrendingScore,
            config.ScoreVersion,
            now,
            cancellationToken);

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.Rising,
            DiscoveryPeriodTypes.Day,
            scores.Where(s => s.TrendState is DiscoveryTrendStates.Rising or DiscoveryTrendStates.Hot)
                .OrderByDescending(s => s.TrendPercentage).ThenByDescending(s => s.TrendingScore).ThenBy(s => s.GameId),
            s => s.TrendPercentage,
            config.ScoreVersion,
            now,
            cancellationToken);

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.Popular,
            DiscoveryPeriodTypes.Day,
            scores.OrderByDescending(s => s.PopularityScore).ThenByDescending(s => s.Score).ThenBy(s => s.GameId),
            s => s.PopularityScore,
            config.ScoreVersion,
            now,
            cancellationToken);

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.MostPlayed,
            DiscoveryPeriodTypes.Day,
            signals.Values.OrderByDescending(s => s.ValidSessionsLifetime).ThenByDescending(s => s.ValidSessions24h).ThenBy(s => s.GameId)
                .Select(s => scores.FirstOrDefault(x => x.GameId == s.GameId) ?? Cold(s.GameId, s.ValidSessionsLifetime)),
            s => s.Score,
            config.ScoreVersion,
            now,
            cancellationToken);

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.MostFavorited,
            DiscoveryPeriodTypes.Day,
            signals.Values.OrderByDescending(s => s.FavoritesLifetime).ThenBy(s => s.GameId)
                .Select(s => scores.FirstOrDefault(x => x.GameId == s.GameId) ?? Cold(s.GameId, s.FavoritesLifetime)),
            s => s.Score,
            config.ScoreVersion,
            now,
            cancellationToken);

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.MostRated,
            DiscoveryPeriodTypes.Day,
            signals.Values.OrderByDescending(s => s.RatingsLifetime).ThenByDescending(s => s.AvgRating).ThenBy(s => s.GameId)
                .Select(s => scores.FirstOrDefault(x => x.GameId == s.GameId) ?? Cold(s.GameId, s.RatingsLifetime)),
            s => s.Score,
            config.ScoreVersion,
            now,
            cancellationToken);

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.MostReviewed,
            DiscoveryPeriodTypes.Day,
            signals.Values.OrderByDescending(s => s.ReviewsLifetime).ThenBy(s => s.GameId)
                .Select(s => scores.FirstOrDefault(x => x.GameId == s.GameId) ?? Cold(s.GameId, s.ReviewsLifetime)),
            s => s.Score,
            config.ScoreVersion,
            now,
            cancellationToken);

        var newTrending = scores
            .Where(s =>
            {
                if (!signals.TryGetValue(s.GameId, out var sig))
                {
                    return false;
                }

                var published = sig.PublishedAt ?? sig.CreatedAt;
                var age = (now - published).TotalDays;
                return age <= config.NewGameDays
                       && sig.ValidSessions24h >= config.MinValidSessionsForNewTrending
                       && s.TrendPercentage >= 0;
            })
            .OrderByDescending(s => s.TrendingScore)
            .ThenByDescending(s => s.FreshnessScore)
            .ThenBy(s => s.GameId);

        await BuildSnapshotAsync(
            DiscoveryRankingTypes.NewTrending,
            DiscoveryPeriodTypes.Day,
            newTrending,
            s => s.TrendingScore,
            config.ScoreVersion,
            now,
            cancellationToken);

        await BustCachesAsync(cancellationToken);
        logger.LogInformation("TrendingCalculationCompleted at={At}", now);
    }

    public async Task<DiscoveryRankingResponse> GetRankingAsync(
        string type,
        string period,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var rankingType = NormalizeType(type);
        var periodType = NormalizePeriod(period);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var cacheKey = $"discovery:ranking:{rankingType}:{periodType}:{category ?? "all"}:{page}:{pageSize}";
        var cached = await cache.GetAsync<DiscoveryRankingResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var (items, total) = await store.GetRankingAsync(
            rankingType, periodType, category, page, pageSize, cancellationToken);

        // Fallback when snapshots empty: serve from current scores
        if (items.Count == 0 && rankingType == DiscoveryRankingTypes.Trending)
        {
            var ids = await store.GetTopTrendingGameIdsAsync(pageSize, cancellationToken);
            items = ids.Select((id, idx) => new DiscoveryRankingItemDto(
                idx + 1, 0, DiscoveryTrendStates.Stable, 0, null, 0,
                new DiscoveryGameCardDto(id, id.ToString("N")[..8], "Game", null, null, null, null, null, null))).ToList();
            // Prefer empty over fake titles — leave empty if no scores
            if (ids.Count == 0)
            {
                items = [];
            }
            else
            {
                // Re-query proper ranking path after scores exist; for cold DB return empty
                items = [];
            }
        }

        var response = new DiscoveryRankingResponse(rankingType, periodType, items, page, pageSize, total);
        await cache.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheTtlMinutes)),
            cancellationToken);
        return response;
    }

    public Task<IReadOnlyList<Guid>> GetTrendingGameIdsAsync(int limit, CancellationToken cancellationToken = default) =>
        store.GetTopTrendingGameIdsAsync(Math.Clamp(limit, 1, 50), cancellationToken);

    private async Task BuildSnapshotAsync(
        string rankingType,
        string periodType,
        IEnumerable<DiscoveryScoreEntity> ordered,
        Func<DiscoveryScoreEntity, double> scoreSelector,
        int scoreVersion,
        DateTimeOffset calculatedAt,
        CancellationToken cancellationToken)
    {
        var previous = await store.GetPreviousRanksAsync(rankingType, periodType, cancellationToken);
        var limit = Math.Max(20, options.Value.RankingLimit);
        var rows = new List<(Guid, double, string, double, int, int?, int)>();
        var rank = 1;
        foreach (var item in ordered.Take(limit))
        {
            previous.TryGetValue(item.GameId, out var prev);
            int? previousRank = prev > 0 ? prev : null;
            var change = previousRank is null ? 0 : previousRank.Value - rank;
            rows.Add((
                item.GameId,
                scoreSelector(item),
                item.TrendState,
                item.TrendPercentage,
                rank,
                previousRank,
                change));
            rank++;
        }

        await store.InsertSnapshotsAsync(rankingType, periodType, scoreVersion, calculatedAt, rows, cancellationToken);
        logger.LogInformation(
            "RankingSnapshotCreated type={Type} period={Period} count={Count}",
            rankingType,
            periodType,
            rows.Count);
    }

    private async Task BustCachesAsync(CancellationToken cancellationToken)
    {
        foreach (var type in DiscoveryRankingTypes.All)
        {
            await cache.RemoveAsync($"discovery:ranking:{type}:DAY:all:1:20", cancellationToken);
            await cache.RemoveAsync($"discovery:ranking:{type}:DAY:all:1:12", cancellationToken);
        }
    }

    private static DiscoveryScoreEntity Cold(Guid gameId, double proxy) =>
        new()
        {
            GameId = gameId,
            Score = Math.Min(100, proxy),
            TrendState = DiscoveryTrendStates.Stable,
            TrendPercentage = 0
        };

    private static string NormalizeType(string? type) =>
        string.IsNullOrWhiteSpace(type)
            ? DiscoveryRankingTypes.Trending
            : DiscoveryRankingTypes.All.Contains(type)
                ? type.ToUpperInvariant()
                : throw new ValidationException("Invalid discovery ranking type.");

    private static string NormalizePeriod(string? period) =>
        (period ?? "24h").Trim().ToUpperInvariant() switch
        {
            "1H" or "HOUR" or "HOURLY" => DiscoveryPeriodTypes.Hour,
            "7D" or "WEEK" or "WEEKLY" => DiscoveryPeriodTypes.Week,
            _ => DiscoveryPeriodTypes.Day
        };
}

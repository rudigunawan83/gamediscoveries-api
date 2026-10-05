using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.DiscoveryScore.Domain;
using GameDiscoveries.Modules.DiscoveryScore.Models;

namespace GameDiscoveries.Modules.DiscoveryScore.Data;

public interface IDiscoveryScoreStore
{
    Task<DiscoveryConfigEntity> GetConfigAsync(CancellationToken cancellationToken = default);

    Task<DiscoveryConfigEntity> UpdateConfigAsync(
        UpsertDiscoveryConfigRequest request,
        Guid adminId,
        int nextVersion,
        CancellationToken cancellationToken = default);

    Task UpsertMetricAsync(
        Guid gameId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string windowType,
        GameSignalRow signal,
        bool use24h,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSignalRow>> LoadSignalsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task UpsertScoreAsync(DiscoveryScoreEntity score, CancellationToken cancellationToken = default);

    Task InsertScoreHistoryAsync(DiscoveryScoreEntity score, CancellationToken cancellationToken = default);

    Task InsertSnapshotsAsync(
        string rankingType,
        string periodType,
        int scoreVersion,
        DateTimeOffset calculatedAt,
        IReadOnlyList<(Guid GameId, double Score, string TrendState, double TrendPercentage, int Rank, int? PreviousRank, int RankChange)> rows,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetPreviousRanksAsync(
        string rankingType,
        string periodType,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<DiscoveryRankingItemDto> Items, int Total)> GetRankingAsync(
        string rankingType,
        string periodType,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DiscoveryScoreEntity?> GetScoreAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveryScoreEntity>> GetScoreHistoryAsync(
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<AdminDiscoveryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetTopTrendingGameIdsAsync(int limit, CancellationToken cancellationToken = default);

    Task CleanupRetentionAsync(int hourlyDays, int dailyDays, CancellationToken cancellationToken = default);
}

public sealed class DiscoveryScoreStore(IDbConnectionFactory connectionFactory) : IDiscoveryScoreStore
{
    public async Task<DiscoveryConfigEntity> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DiscoveryConfigEntity>(new CommandDefinition(
            """
            SELECT id AS Id, score_version AS ScoreVersion,
                   popularity_weight AS PopularityWeight, engagement_weight AS EngagementWeight,
                   quality_weight AS QualityWeight, momentum_weight AS MomentumWeight,
                   growth_weight AS GrowthWeight, freshness_weight AS FreshnessWeight,
                   trending_recent_weight AS TrendingRecentWeight, trending_momentum_weight AS TrendingMomentumWeight,
                   trending_growth_weight AS TrendingGrowthWeight, trending_engagement_weight AS TrendingEngagementWeight,
                   trending_freshness_weight AS TrendingFreshnessWeight,
                   freshness_decay_days AS FreshnessDecayDays, new_game_days AS NewGameDays,
                   min_valid_sessions_new_trending AS MinValidSessionsForNewTrending,
                   growth_smoothing AS GrowthSmoothing, bayesian_m AS BayesianM,
                   rising_growth_threshold AS RisingGrowthThreshold,
                   declining_growth_threshold AS DecliningGrowthThreshold,
                   score_valid_minutes AS ScoreValidMinutes,
                   updated_at AS UpdatedAt, updated_by AS UpdatedBy
            FROM discovery_score_config
            ORDER BY updated_at DESC
            LIMIT 1
            """,
            cancellationToken: cancellationToken));

        return row ?? new DiscoveryConfigEntity
        {
            Id = Guid.Parse("b1000008-0001-4000-8000-000000000001"),
            ScoreVersion = 1,
            PopularityWeight = 0.20,
            EngagementWeight = 0.25,
            QualityWeight = 0.15,
            MomentumWeight = 0.20,
            GrowthWeight = 0.10,
            FreshnessWeight = 0.10,
            TrendingRecentWeight = 0.30,
            TrendingMomentumWeight = 0.30,
            TrendingGrowthWeight = 0.20,
            TrendingEngagementWeight = 0.15,
            TrendingFreshnessWeight = 0.05,
            FreshnessDecayDays = 30,
            NewGameDays = 14,
            MinValidSessionsForNewTrending = 3,
            GrowthSmoothing = 10,
            BayesianM = 20,
            RisingGrowthThreshold = 25,
            DecliningGrowthThreshold = -20,
            ScoreValidMinutes = 90,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<DiscoveryConfigEntity> UpdateConfigAsync(
        UpsertDiscoveryConfigRequest request,
        Guid adminId,
        int nextVersion,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE discovery_score_config SET
                score_version = @ScoreVersion,
                popularity_weight = @PopularityWeight,
                engagement_weight = @EngagementWeight,
                quality_weight = @QualityWeight,
                momentum_weight = @MomentumWeight,
                growth_weight = @GrowthWeight,
                freshness_weight = @FreshnessWeight,
                trending_recent_weight = @TrendingRecentWeight,
                trending_momentum_weight = @TrendingMomentumWeight,
                trending_growth_weight = @TrendingGrowthWeight,
                trending_engagement_weight = @TrendingEngagementWeight,
                trending_freshness_weight = @TrendingFreshnessWeight,
                freshness_decay_days = @FreshnessDecayDays,
                new_game_days = @NewGameDays,
                min_valid_sessions_new_trending = @MinValidSessionsForNewTrending,
                growth_smoothing = @GrowthSmoothing,
                bayesian_m = @BayesianM,
                rising_growth_threshold = @RisingGrowthThreshold,
                declining_growth_threshold = @DecliningGrowthThreshold,
                score_valid_minutes = @ScoreValidMinutes,
                updated_at = (NOW() AT TIME ZONE 'utc'),
                updated_by = @AdminId
            WHERE id = (SELECT id FROM discovery_score_config ORDER BY updated_at DESC LIMIT 1)
            """,
            new
            {
                ScoreVersion = nextVersion,
                request.PopularityWeight,
                request.EngagementWeight,
                request.QualityWeight,
                request.MomentumWeight,
                request.GrowthWeight,
                request.FreshnessWeight,
                request.TrendingRecentWeight,
                request.TrendingMomentumWeight,
                request.TrendingGrowthWeight,
                request.TrendingEngagementWeight,
                request.TrendingFreshnessWeight,
                request.FreshnessDecayDays,
                request.NewGameDays,
                request.MinValidSessionsForNewTrending,
                request.GrowthSmoothing,
                request.BayesianM,
                request.RisingGrowthThreshold,
                request.DecliningGrowthThreshold,
                request.ScoreValidMinutes,
                AdminId = adminId
            },
            cancellationToken: cancellationToken));

        return await GetConfigAsync(cancellationToken);
    }

    public async Task UpsertMetricAsync(
        Guid gameId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string windowType,
        GameSignalRow signal,
        bool use24h,
        CancellationToken cancellationToken = default)
    {
        var views = use24h ? signal.Views24h : signal.Views24h;
        var starts = use24h ? signal.Starts24h : signal.Starts24h;
        var sessions = use24h ? signal.Sessions24h : signal.Sessions24h;
        var valid = use24h ? signal.ValidSessions24h : signal.ValidSessions24h;
        var active = use24h ? signal.ActiveSeconds24h : signal.ActiveSeconds24h;
        var unique = use24h ? signal.UniqueUsers24h : signal.UniqueUsers24h;
        var favorites = use24h ? signal.Favorites24h : signal.FavoritesLifetime;
        var avgSession = valid > 0 ? (double)active / valid : 0;
        var avgPerUser = unique > 0 ? (double)sessions / unique : 0;
        var favoriteRate = unique > 0 ? (double)favorites / unique : 0;
        var engagementRate = sessions > 0 ? (double)valid / sessions : 0;

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO game_discovery_metrics (
                id, game_id, window_start, window_end, window_type,
                views, starts, sessions, valid_sessions, active_seconds, unique_users,
                favorites, ratings, reviews, shares, returning_users,
                avg_session_seconds, avg_sessions_per_user, favorite_rate, rating_rate,
                review_rate, share_rate, engagement_rate, created_at, updated_at)
            VALUES (
                @Id, @GameId, @WindowStart, @WindowEnd, @WindowType,
                @Views, @Starts, @Sessions, @ValidSessions, @ActiveSeconds, @UniqueUsers,
                @Favorites, @Ratings, @Reviews, 0, @ReturningUsers,
                @AvgSession, @AvgPerUser, @FavoriteRate, 0, 0, 0, @EngagementRate,
                (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (game_id, window_start, window_type) DO UPDATE SET
                window_end = EXCLUDED.window_end,
                views = EXCLUDED.views,
                starts = EXCLUDED.starts,
                sessions = EXCLUDED.sessions,
                valid_sessions = EXCLUDED.valid_sessions,
                active_seconds = EXCLUDED.active_seconds,
                unique_users = EXCLUDED.unique_users,
                favorites = EXCLUDED.favorites,
                ratings = EXCLUDED.ratings,
                reviews = EXCLUDED.reviews,
                returning_users = EXCLUDED.returning_users,
                avg_session_seconds = EXCLUDED.avg_session_seconds,
                avg_sessions_per_user = EXCLUDED.avg_sessions_per_user,
                favorite_rate = EXCLUDED.favorite_rate,
                engagement_rate = EXCLUDED.engagement_rate,
                updated_at = (NOW() AT TIME ZONE 'utc')
            """,
            new
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                WindowType = windowType,
                Views = views,
                Starts = starts,
                Sessions = sessions,
                ValidSessions = valid,
                ActiveSeconds = active,
                UniqueUsers = unique,
                Favorites = favorites,
                Ratings = signal.RatingsLifetime,
                Reviews = signal.ReviewsLifetime,
                ReturningUsers = signal.ReturningUsers24h,
                AvgSession = avgSession,
                AvgPerUser = avgPerUser,
                FavoriteRate = favoriteRate,
                EngagementRate = engagementRate
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<GameSignalRow>> LoadSignalsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var currentStart = now.AddHours(-24);
        var prevStart = now.AddHours(-48);
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<GameSignalRow>(new CommandDefinition(
            """
            SELECT
                g.id AS GameId,
                g.published_at AS PublishedAt,
                g.created_at AS CreatedAt,
                COALESCE((
                    SELECT COUNT(*)::int FROM analytics_events e
                    WHERE e.game_id = g.id
                      AND e.event_type IN ('GAME_VIEW', 'GAME_VIEWED')
                      AND e.occurred_at >= @CurrentStart AND e.occurred_at < @Now
                ), 0) AS Views24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM analytics_events e
                    WHERE e.game_id = g.id
                      AND e.event_type IN ('GAME_VIEW', 'GAME_VIEWED')
                      AND e.occurred_at >= @PrevStart AND e.occurred_at < @CurrentStart
                ), 0) AS ViewsPrev24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM analytics_events e
                    WHERE e.game_id = g.id
                      AND e.event_type IN ('GAME_START', 'GAME_SESSION_START')
                      AND e.occurred_at >= @CurrentStart AND e.occurred_at < @Now
                ), 0) AS Starts24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM analytics_events e
                    WHERE e.game_id = g.id
                      AND e.event_type IN ('GAME_START', 'GAME_SESSION_START')
                      AND e.occurred_at >= @PrevStart AND e.occurred_at < @CurrentStart
                ), 0) AS StartsPrev24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.ended_at >= @CurrentStart AND s.ended_at < @Now
                ), 0) AS Sessions24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE
                      AND s.ended_at >= @CurrentStart AND s.ended_at < @Now
                ), 0) AS ValidSessions24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE
                      AND s.ended_at >= @PrevStart AND s.ended_at < @CurrentStart
                ), 0) AS ValidSessionsPrev24h,
                COALESCE((
                    SELECT COALESCE(SUM(s.active_seconds), 0)::bigint FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE
                      AND s.ended_at >= @CurrentStart AND s.ended_at < @Now
                ), 0) AS ActiveSeconds24h,
                COALESCE((
                    SELECT COUNT(DISTINCT s.user_id)::int FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE AND s.user_id IS NOT NULL
                      AND s.ended_at >= @CurrentStart AND s.ended_at < @Now
                ), 0) AS UniqueUsers24h,
                COALESCE((
                    SELECT COUNT(DISTINCT s.user_id)::int FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE AND s.user_id IS NOT NULL
                      AND s.ended_at >= @PrevStart AND s.ended_at < @CurrentStart
                ), 0) AS UniqueUsersPrev24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM user_favorites f
                    WHERE f.game_id = g.id AND f.created_at >= @CurrentStart AND f.created_at < @Now
                ), 0) AS Favorites24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM user_favorites f
                    WHERE f.game_id = g.id AND f.created_at >= @PrevStart AND f.created_at < @CurrentStart
                ), 0) AS FavoritesPrev24h,
                COALESCE((SELECT COUNT(*)::int FROM user_favorites f WHERE f.game_id = g.id), 0) AS FavoritesLifetime,
                COALESCE((
                    SELECT COUNT(*)::int FROM game_reviews r
                    WHERE r.game_id = g.id AND r.status = 'published' AND r.deleted_at IS NULL AND r.rating > 0
                ), 0) AS RatingsLifetime,
                COALESCE((
                    SELECT COUNT(*)::int FROM game_reviews r
                    WHERE r.game_id = g.id AND r.status = 'published' AND r.deleted_at IS NULL
                      AND NULLIF(BTRIM(r.content), '') IS NOT NULL
                ), 0) AS ReviewsLifetime,
                COALESCE((
                    SELECT AVG(r.rating)::float8 FROM game_reviews r
                    WHERE r.game_id = g.id AND r.status = 'published' AND r.deleted_at IS NULL AND r.rating > 0
                ), 0) AS AvgRating,
                COALESCE((
                    SELECT COUNT(DISTINCT s.user_id)::int
                    FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE AND s.user_id IS NOT NULL
                      AND s.ended_at >= @CurrentStart AND s.ended_at < @Now
                      AND EXISTS (
                          SELECT 1 FROM game_play_sessions p
                          WHERE p.game_id = g.id AND p.user_id = s.user_id AND p.is_valid = TRUE
                            AND p.ended_at < @CurrentStart
                      )
                ), 0) AS ReturningUsers24h,
                COALESCE((
                    SELECT COUNT(*)::int FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE
                ), 0) AS ValidSessionsLifetime,
                COALESCE((
                    SELECT COALESCE(SUM(s.active_seconds), 0)::bigint FROM game_play_sessions s
                    WHERE s.game_id = g.id AND s.is_valid = TRUE
                ), 0) AS ActiveSecondsLifetime
            FROM games g
            WHERE g.status = 'published'
            """,
            new { Now = now, CurrentStart = currentStart, PrevStart = prevStart },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task UpsertScoreAsync(DiscoveryScoreEntity score, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO game_discovery_scores (
                id, game_id, score, popularity_score, engagement_score, quality_score,
                momentum_score, growth_score, freshness_score, trending_score,
                trend_state, trend_percentage, score_version, calculated_at, valid_until,
                created_at, updated_at)
            VALUES (
                @Id, @GameId, @Score, @PopularityScore, @EngagementScore, @QualityScore,
                @MomentumScore, @GrowthScore, @FreshnessScore, @TrendingScore,
                @TrendState, @TrendPercentage, @ScoreVersion, @CalculatedAt, @ValidUntil,
                (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (game_id) DO UPDATE SET
                score = EXCLUDED.score,
                popularity_score = EXCLUDED.popularity_score,
                engagement_score = EXCLUDED.engagement_score,
                quality_score = EXCLUDED.quality_score,
                momentum_score = EXCLUDED.momentum_score,
                growth_score = EXCLUDED.growth_score,
                freshness_score = EXCLUDED.freshness_score,
                trending_score = EXCLUDED.trending_score,
                trend_state = EXCLUDED.trend_state,
                trend_percentage = EXCLUDED.trend_percentage,
                score_version = EXCLUDED.score_version,
                calculated_at = EXCLUDED.calculated_at,
                valid_until = EXCLUDED.valid_until,
                updated_at = (NOW() AT TIME ZONE 'utc')
            """,
            score,
            cancellationToken: cancellationToken));
    }

    public async Task InsertScoreHistoryAsync(DiscoveryScoreEntity score, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO game_discovery_score_history (
                id, game_id, discovery_score, popularity_score, engagement_score, quality_score,
                momentum_score, growth_score, freshness_score, trending_score,
                trend_state, trend_percentage, score_version, calculated_at)
            VALUES (
                @Id, @GameId, @Score, @PopularityScore, @EngagementScore, @QualityScore,
                @MomentumScore, @GrowthScore, @FreshnessScore, @TrendingScore,
                @TrendState, @TrendPercentage, @ScoreVersion, @CalculatedAt)
            """,
            new
            {
                Id = Guid.NewGuid(),
                score.GameId,
                score.Score,
                score.PopularityScore,
                score.EngagementScore,
                score.QualityScore,
                score.MomentumScore,
                score.GrowthScore,
                score.FreshnessScore,
                score.TrendingScore,
                score.TrendState,
                score.TrendPercentage,
                score.ScoreVersion,
                score.CalculatedAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task InsertSnapshotsAsync(
        string rankingType,
        string periodType,
        int scoreVersion,
        DateTimeOffset calculatedAt,
        IReadOnlyList<(Guid GameId, double Score, string TrendState, double TrendPercentage, int Rank, int? PreviousRank, int RankChange)> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO game_trending_snapshots (
                    id, game_id, ranking_type, period_type, score, rank, previous_rank,
                    rank_change, trend_state, trend_percentage, score_version, calculated_at)
                VALUES (
                    @Id, @GameId, @RankingType, @PeriodType, @Score, @Rank, @PreviousRank,
                    @RankChange, @TrendState, @TrendPercentage, @ScoreVersion, @CalculatedAt)
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    row.GameId,
                    RankingType = rankingType,
                    PeriodType = periodType,
                    row.Score,
                    row.Rank,
                    row.PreviousRank,
                    row.RankChange,
                    row.TrendState,
                    row.TrendPercentage,
                    ScoreVersion = scoreVersion,
                    CalculatedAt = calculatedAt
                },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetPreviousRanksAsync(
        string rankingType,
        string periodType,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var batch = await connection.QuerySingleOrDefaultAsync<DateTimeOffset?>(new CommandDefinition(
            """
            SELECT MAX(calculated_at)
            FROM game_trending_snapshots
            WHERE ranking_type = @RankingType AND period_type = @PeriodType
            """,
            new { RankingType = rankingType, PeriodType = periodType },
            cancellationToken: cancellationToken));

        if (batch is null)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await connection.QueryAsync<(Guid GameId, int Rank)>(new CommandDefinition(
            """
            SELECT game_id AS GameId, rank AS Rank
            FROM game_trending_snapshots
            WHERE ranking_type = @RankingType AND period_type = @PeriodType AND calculated_at = @Batch
            """,
            new { RankingType = rankingType, PeriodType = periodType, Batch = batch },
            cancellationToken: cancellationToken));

        return rows.ToDictionary(x => x.GameId, x => x.Rank);
    }

    public async Task<(IReadOnlyList<DiscoveryRankingItemDto> Items, int Total)> GetRankingAsync(
        string rankingType,
        string periodType,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var batch = await connection.QuerySingleOrDefaultAsync<DateTimeOffset?>(new CommandDefinition(
            """
            SELECT MAX(calculated_at)
            FROM game_trending_snapshots
            WHERE ranking_type = @RankingType AND period_type = @PeriodType
            """,
            new { RankingType = rankingType, PeriodType = periodType },
            cancellationToken: cancellationToken));

        if (batch is null)
        {
            return ([], 0);
        }

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM game_trending_snapshots s
            INNER JOIN games g ON g.id = s.game_id
            LEFT JOIN LATERAL (
                SELECT c.name FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id ORDER BY c.name LIMIT 1
            ) cat ON TRUE
            WHERE s.ranking_type = @RankingType AND s.period_type = @PeriodType AND s.calculated_at = @Batch
              AND g.status = 'published'
              AND (@Category IS NULL OR cat.name ILIKE @Category)
            """,
            new { RankingType = rankingType, PeriodType = periodType, Batch = batch, Category = category },
            cancellationToken: cancellationToken));

        var offset = Math.Max(0, (page - 1) * pageSize);
        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT s.rank AS Rank, s.score AS Score, s.trend_state AS Trend, s.trend_percentage AS TrendPercentage,
                   s.previous_rank AS PreviousRank, s.rank_change AS RankChange,
                   g.id AS Id, g.slug AS Slug, g.title AS Title, g.description AS Description,
                   g.thumbnail_url AS ThumbnailUrl, g.cover_url AS CoverUrl,
                   cat.name AS Category, g.published_at AS PublishedAt,
                   (
                       SELECT AVG(r.rating)::float8 FROM game_reviews r
                       WHERE r.game_id = g.id AND r.status = 'published' AND r.deleted_at IS NULL AND r.rating > 0
                   ) AS AverageRating
            FROM game_trending_snapshots s
            INNER JOIN games g ON g.id = s.game_id
            LEFT JOIN LATERAL (
                SELECT c.name FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id ORDER BY c.name LIMIT 1
            ) cat ON TRUE
            WHERE s.ranking_type = @RankingType AND s.period_type = @PeriodType AND s.calculated_at = @Batch
              AND g.status = 'published'
              AND (@Category IS NULL OR cat.name ILIKE @Category)
            ORDER BY s.rank
            OFFSET @Offset LIMIT @Limit
            """,
            new
            {
                RankingType = rankingType,
                PeriodType = periodType,
                Batch = batch,
                Category = category,
                Offset = offset,
                Limit = pageSize
            },
            cancellationToken: cancellationToken));

        var items = rows.Select(r => new DiscoveryRankingItemDto(
            (int)r.rank,
            (double)r.score,
            (string)r.trend,
            (double)r.trendpercentage,
            (int?)r.previousrank,
            (int)r.rankchange,
            new DiscoveryGameCardDto(
                (Guid)r.id,
                (string)r.slug,
                (string)r.title,
                (string?)r.description,
                (string?)r.thumbnailurl,
                (string?)r.coverurl,
                (string?)r.category,
                (double?)r.averagerating,
                (DateTimeOffset?)r.publishedat))).ToList();

        return (items, total);
    }

    public async Task<DiscoveryScoreEntity?> GetScoreAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DiscoveryScoreEntity>(new CommandDefinition(
            """
            SELECT id AS Id, game_id AS GameId, score AS Score,
                   popularity_score AS PopularityScore, engagement_score AS EngagementScore,
                   quality_score AS QualityScore, momentum_score AS MomentumScore,
                   growth_score AS GrowthScore, freshness_score AS FreshnessScore,
                   trending_score AS TrendingScore, trend_state AS TrendState,
                   trend_percentage AS TrendPercentage, score_version AS ScoreVersion,
                   calculated_at AS CalculatedAt, valid_until AS ValidUntil
            FROM game_discovery_scores
            WHERE game_id = @GameId
            """,
            new { GameId = gameId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DiscoveryScoreEntity>> GetScoreHistoryAsync(
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DiscoveryScoreEntity>(new CommandDefinition(
            """
            SELECT id AS Id, game_id AS GameId, discovery_score AS Score,
                   popularity_score AS PopularityScore, engagement_score AS EngagementScore,
                   quality_score AS QualityScore, momentum_score AS MomentumScore,
                   growth_score AS GrowthScore, freshness_score AS FreshnessScore,
                   trending_score AS TrendingScore, trend_state AS TrendState,
                   trend_percentage AS TrendPercentage, score_version AS ScoreVersion,
                   calculated_at AS CalculatedAt
            FROM game_discovery_score_history
            WHERE game_id = @GameId
            ORDER BY calculated_at DESC
            LIMIT @Limit
            """,
            new { GameId = gameId, Limit = Math.Clamp(limit, 1, 100) },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<AdminDiscoveryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync(new CommandDefinition(
            """
            SELECT
                COUNT(*)::bigint AS ScoredGames,
                COALESCE(AVG(score), 0)::float8 AS AverageDiscoveryScore,
                COALESCE(AVG(trending_score), 0)::float8 AS AverageTrendingScore,
                COUNT(*) FILTER (WHERE trend_state = 'RISING')::bigint AS RisingGames,
                COUNT(*) FILTER (WHERE trend_state = 'HOT')::bigint AS HotGames,
                COUNT(*) FILTER (WHERE trend_state = 'DECLINING')::bigint AS DecliningGames,
                COUNT(*) FILTER (WHERE trend_state = 'NEW')::bigint AS NewGames,
                MAX(calculated_at) AS LastCalculatedAt,
                COALESCE(MAX(score_version), 1)::int AS ScoreVersion
            FROM game_discovery_scores
            """,
            cancellationToken: cancellationToken));

        return new AdminDiscoveryOverviewDto(
            (long)row.scoredgames,
            Math.Round((double)row.averagediscoveryscore, 2),
            Math.Round((double)row.averagetrendingscore, 2),
            (long)row.risinggames,
            (long)row.hotgames,
            (long)row.declininggames,
            (long)row.newgames,
            (DateTimeOffset?)row.lastcalculatedat,
            (int)row.scoreversion);
    }

    public async Task<IReadOnlyList<Guid>> GetTopTrendingGameIdsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var batch = await connection.QuerySingleOrDefaultAsync<DateTimeOffset?>(new CommandDefinition(
            """
            SELECT MAX(calculated_at) FROM game_trending_snapshots
            WHERE ranking_type = 'TRENDING' AND period_type = 'DAY'
            """,
            cancellationToken: cancellationToken));

        if (batch is null)
        {
            var fallback = await connection.QueryAsync<Guid>(new CommandDefinition(
                """
                SELECT game_id FROM game_discovery_scores
                ORDER BY trending_score DESC, score DESC, game_id
                LIMIT @Limit
                """,
                new { Limit = limit },
                cancellationToken: cancellationToken));
            return fallback.ToList();
        }

        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT s.game_id
            FROM game_trending_snapshots s
            INNER JOIN games g ON g.id = s.game_id
            WHERE s.ranking_type = 'TRENDING' AND s.period_type = 'DAY' AND s.calculated_at = @Batch
              AND g.status = 'published'
            ORDER BY s.rank
            LIMIT @Limit
            """,
            new { Batch = batch, Limit = limit },
            cancellationToken: cancellationToken));
        return ids.ToList();
    }

    public async Task CleanupRetentionAsync(int hourlyDays, int dailyDays, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM game_discovery_metrics
            WHERE window_type = 'HOURLY' AND window_start < (NOW() AT TIME ZONE 'utc') - (@HourlyDays || ' days')::interval;
            DELETE FROM game_discovery_metrics
            WHERE window_type = 'DAILY' AND window_start < (NOW() AT TIME ZONE 'utc') - (@DailyDays || ' days')::interval;
            DELETE FROM game_discovery_score_history
            WHERE calculated_at < (NOW() AT TIME ZONE 'utc') - (@DailyDays || ' days')::interval;
            DELETE FROM game_trending_snapshots
            WHERE calculated_at < (NOW() AT TIME ZONE 'utc') - INTERVAL '30 days';
            """,
            new { HourlyDays = hourlyDays, DailyDays = dailyDays },
            cancellationToken: cancellationToken));
    }
}

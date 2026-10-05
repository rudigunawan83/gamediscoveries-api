using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.Modules.Recommendation.Data;

public interface IRecommendationRepository
{
    Task<UserPreferenceProfile> BuildProfileAsync(Guid? userId, CancellationToken cancellationToken = default);
    Task<CandidateGame?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateGame>> GetCandidatesAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateGame>> GetByFeedAsync(
        string feedType,
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateGame>> GetNewestAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateGame>> GetTrendingAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateGame>> GetHiddenGemCandidatesAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateGame>> GetSimilarCandidatesAsync(
        CandidateGame seed,
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default);
}

public sealed class RecommendationRepository(IDbConnectionFactory connectionFactory) : IRecommendationRepository
{
    private const string CandidateSelect = """
        SELECT
            g.id AS Id,
            g.slug AS Slug,
            g.title AS Title,
            g.description AS Description,
            g.thumbnail_url AS ThumbnailUrl,
            cat.name AS Category,
            g.orientation AS Orientation,
            g.platform AS Platform,
            g.mobile_ready AS MobileReady,
            EXISTS (
                SELECT 1 FROM game_feed_memberships mpx
                WHERE mpx.game_id = g.id AND mpx.feed_type IN ('Multiplayer', 'TwoPlayer')
            ) AS Multiplayer,
            g.published_at AS PublishedAt,
            COALESCE((
                SELECT COUNT(*)::float
                FROM game_feed_memberships fm
                WHERE fm.game_id = g.id
            ), 0) AS PopularityProxy,
            COALESCE((
                SELECT SUM(h.duration_seconds)::float
                FROM user_play_history h
                WHERE h.game_id = g.id
            ), 0) AS EngagementProxy,
            COALESCE((
                SELECT COUNT(*)::int
                FROM user_play_history h2
                WHERE h2.game_id = g.id
            ), 0) AS GlobalPlaySessions,
            COALESCE(ds.score, 0)::float8 AS DiscoveryScore,
            COALESCE(ds.trending_score, 0)::float8 AS TrendingScore,
            COALESCE(ds.freshness_score, 0)::float8 AS FreshnessScore,
            COALESCE(ds.momentum_score, 0)::float8 AS MomentumScore,
            COALESCE((
                SELECT string_agg(DISTINCT t.tag, '|')
                FROM (
                    SELECT LOWER(gt.tag) AS tag FROM game_tags gt WHERE gt.game_id = g.id
                    UNION
                    SELECT LOWER(tg.name) AS tag
                    FROM game_tag_links gtl
                    INNER JOIN tags tg ON tg.id = gtl.tag_id
                    WHERE gtl.game_id = g.id
                ) t
            ), '') AS TagsJoined
        """;

    private const string PublishedFilter = """
        FROM games g
        LEFT JOIN game_discovery_scores ds ON ds.game_id = g.id
        LEFT JOIN LATERAL (
            SELECT c.name
            FROM game_categories gc
            INNER JOIN categories c ON c.id = gc.category_id
            WHERE gc.game_id = g.id
            ORDER BY c.name
            LIMIT 1
        ) cat ON TRUE
        WHERE g.status = 'published'
          AND COALESCE((
                SELECT m.availability_status
                FROM game_provider_mappings m
                WHERE m.game_id = g.id
                ORDER BY m.updated_at DESC NULLS LAST
                LIMIT 1
              ), 'active') NOT IN ('blocked', 'inactive')
        """;

    public async Task<UserPreferenceProfile> BuildProfileAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return new UserPreferenceProfile();
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        const string favoritesSql = """
            SELECT f.game_id AS GameId, f.created_at AS At, cat.name AS Category,
                   COALESCE((
                       SELECT string_agg(DISTINCT LOWER(gt.tag), '|')
                       FROM game_tags gt WHERE gt.game_id = g.id
                   ), '') AS TagsJoined
            FROM user_favorites f
            INNER JOIN games g ON g.id = f.game_id
            LEFT JOIN LATERAL (
                SELECT c.name FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id ORDER BY c.name LIMIT 1
            ) cat ON TRUE
            WHERE f.user_id = @UserId AND g.status = 'published'
            ORDER BY f.created_at DESC
            LIMIT 50;
            """;

        const string historySql = """
            SELECT h.game_id AS GameId, h.played_at AS At, h.duration_seconds AS DurationSeconds,
                   cat.name AS Category,
                   COALESCE((
                       SELECT string_agg(DISTINCT LOWER(gt.tag), '|')
                       FROM game_tags gt WHERE gt.game_id = g.id
                   ), '') AS TagsJoined
            FROM user_play_history h
            INNER JOIN games g ON g.id = h.game_id
            LEFT JOIN LATERAL (
                SELECT c.name FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id ORDER BY c.name LIMIT 1
            ) cat ON TRUE
            WHERE h.user_id = @UserId AND g.status = 'published'
            ORDER BY h.played_at DESC
            LIMIT 50;
            """;

        var favorites = (await connection.QueryAsync<SignalRow>(
            new CommandDefinition(favoritesSql, new { UserId = userId }, cancellationToken: cancellationToken))).ToList();
        var history = (await connection.QueryAsync<SignalRow>(
            new CommandDefinition(historySql, new { UserId = userId }, cancellationToken: cancellationToken))).ToList();

        var signals = new List<UserSignal>();
        foreach (var fav in favorites)
        {
            signals.Add(new UserSignal(fav.GameId, "favorite", fav.At, 1, 0, fav.Category, SplitTags(fav.TagsJoined)));
        }

        foreach (var play in history)
        {
            var kind = play.DurationSeconds >= 120 ? "completed" : "played";
            signals.Add(new UserSignal(
                play.GameId,
                kind,
                play.At,
                Math.Max(1, play.DurationSeconds / 60),
                play.DurationSeconds,
                play.Category,
                SplitTags(play.TagsJoined)));
        }

        var categories = Aggregate(signals.Where(s => !string.IsNullOrWhiteSpace(s.Category))
            .Select(s => (s.Category!, Weight(s.Kind))));
        var tags = Aggregate(signals.SelectMany(s => s.Tags.Select(t => (t, Weight(s.Kind)))));
        var orientations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var mobilePref = history.Count == 0 ? 0.5 : 0.5;
        var multiplayerPref = signals.Count(s => s.Tags.Any(t => t.Contains("multi", StringComparison.OrdinalIgnoreCase))) > 0
            ? 0.7
            : 0.3;

        var disliked = new List<Guid>();
        try
        {
            disliked = (await connection.QueryAsync<Guid>(new CommandDefinition(
                """
                SELECT DISTINCT game_id
                FROM recommendation_feedback
                WHERE user_id = @UserId
                  AND feedback_type IN ('DISLIKE', 'NOT_INTERESTED')
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            // ignore until migration applied
        }

        try
        {
            var lowRated = (await connection.QueryAsync<Guid>(new CommandDefinition(
                """
                SELECT DISTINCT game_id FROM game_reviews
                WHERE user_id = @UserId AND rating <= 2 AND status = 'published' AND deleted_at IS NULL
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken))).ToList();

            foreach (var id in lowRated.Where(id => !disliked.Contains(id)))
            {
                disliked.Add(id);
            }
        }
        catch
        {
            // ignore
        }

        var totalInteractions = favorites.Count + history.Count;
        var profileLevel = PreferenceSignals.ResolveProfileLevel(totalInteractions);

        // Persist snapshot (best-effort; ignore if migration not applied yet)
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO user_recommendation_profiles (
                    id, user_id, preferred_categories_json, preferred_tags_json,
                    category_scores_json, tag_scores_json, preferred_game_ids_json,
                    disliked_game_ids_json, total_interactions, profile_level, profile_version,
                    last_calculated_at, created_at, updated_at)
                VALUES (
                    @Id, @UserId, @Categories::jsonb, @Tags::jsonb,
                    @Categories::jsonb, @Tags::jsonb, @PreferredGames::jsonb,
                    @Disliked::jsonb, @Total, @Level, 1,
                    (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
                ON CONFLICT (user_id) DO UPDATE SET
                    preferred_categories_json = EXCLUDED.preferred_categories_json,
                    preferred_tags_json = EXCLUDED.preferred_tags_json,
                    category_scores_json = EXCLUDED.category_scores_json,
                    tag_scores_json = EXCLUDED.tag_scores_json,
                    preferred_game_ids_json = EXCLUDED.preferred_game_ids_json,
                    disliked_game_ids_json = EXCLUDED.disliked_game_ids_json,
                    total_interactions = EXCLUDED.total_interactions,
                    profile_level = EXCLUDED.profile_level,
                    last_calculated_at = EXCLUDED.last_calculated_at,
                    updated_at = (NOW() AT TIME ZONE 'utc')
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Categories = System.Text.Json.JsonSerializer.Serialize(categories),
                    Tags = System.Text.Json.JsonSerializer.Serialize(tags),
                    PreferredGames = System.Text.Json.JsonSerializer.Serialize(favorites.Select(f => f.GameId)),
                    Disliked = System.Text.Json.JsonSerializer.Serialize(disliked),
                    Total = totalInteractions,
                    Level = profileLevel
                },
                cancellationToken: cancellationToken));
        }
        catch
        {
            // Table may not exist before migration; profile still works in-memory.
        }

        return new UserPreferenceProfile
        {
            UserId = userId,
            PreferredCategories = categories,
            PreferredTags = tags,
            PreferredOrientations = orientations,
            MobilePreference = mobilePref,
            MultiplayerPreference = multiplayerPref,
            FavoriteGameIds = favorites.Select(f => f.GameId).Distinct().ToList(),
            PlayedGameIds = history.Select(h => h.GameId).Distinct().ToList(),
            DislikedGameIds = disliked,
            RecentSeedGameIds = history.Select(h => h.GameId).Distinct().Take(5).ToList(),
            Signals = signals,
            TotalInteractions = totalInteractions,
            ProfileLevel = profileLevel,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public Task<CandidateGame?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default)
        => QuerySingleAsync($"{CandidateSelect} {PublishedFilter} AND g.id = @GameId LIMIT 1;", new { GameId = gameId }, cancellationToken);

    public Task<IReadOnlyList<CandidateGame>> GetCandidatesAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default)
        => QueryListAsync(
            $"""
             {CandidateSelect}
             {PublishedFilter}
             {ExcludeClause(excludeIds)}
             ORDER BY g.updated_at DESC, g.published_at DESC NULLS LAST
             LIMIT @Limit;
             """,
            new { Limit = limit, ExcludeIds = excludeIds?.ToArray() ?? [] },
            "catalog",
            cancellationToken);

    public Task<IReadOnlyList<CandidateGame>> GetByFeedAsync(
        string feedType,
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default)
        => QueryListAsync(
            $"""
             {CandidateSelect}
             {PublishedFilter}
               AND EXISTS (
                    SELECT 1 FROM game_feed_memberships fm
                    WHERE fm.game_id = g.id AND fm.feed_type = @FeedType
               )
             {ExcludeClause(excludeIds)}
             ORDER BY g.published_at DESC NULLS LAST, g.created_at DESC
             LIMIT @Limit;
             """,
            new { FeedType = feedType, Limit = limit, ExcludeIds = excludeIds?.ToArray() ?? [] },
            feedType.ToLowerInvariant(),
            cancellationToken);

    public Task<IReadOnlyList<CandidateGame>> GetNewestAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default)
        => QueryListAsync(
            $"""
             {CandidateSelect}
             {PublishedFilter}
             {ExcludeClause(excludeIds)}
             ORDER BY g.published_at DESC NULLS LAST, g.created_at DESC
             LIMIT @Limit;
             """,
            new { Limit = limit, ExcludeIds = excludeIds?.ToArray() ?? [] },
            "new",
            cancellationToken);

    public Task<IReadOnlyList<CandidateGame>> GetTrendingAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default)
        => QueryListAsync(
            $"""
             {CandidateSelect}
             {PublishedFilter}
             {ExcludeClause(excludeIds)}
             ORDER BY g.updated_at DESC, g.published_at DESC NULLS LAST
             LIMIT @Limit;
             """,
            new { Limit = limit, ExcludeIds = excludeIds?.ToArray() ?? [] },
            "trending",
            cancellationToken);

    public Task<IReadOnlyList<CandidateGame>> GetHiddenGemCandidatesAsync(
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default)
        => QueryListAsync(
            $"""
             {CandidateSelect}
             {PublishedFilter}
               AND NOT EXISTS (
                    SELECT 1 FROM game_feed_memberships fm
                    WHERE fm.game_id = g.id
                      AND fm.feed_type IN ('Popular', 'HotGames', 'MostPlayed', 'BestGames')
               )
             {ExcludeClause(excludeIds)}
             ORDER BY
                COALESCE((SELECT SUM(h.duration_seconds) FROM user_play_history h WHERE h.game_id = g.id), 0) DESC,
                g.published_at DESC NULLS LAST
             LIMIT @Limit;
             """,
            new { Limit = limit, ExcludeIds = excludeIds?.ToArray() ?? [] },
            "hidden",
            cancellationToken);

    public Task<IReadOnlyList<CandidateGame>> GetSimilarCandidatesAsync(
        CandidateGame seed,
        int limit,
        IEnumerable<Guid>? excludeIds = null,
        CancellationToken cancellationToken = default)
        => QueryListAsync(
            $"""
             {CandidateSelect}
             {PublishedFilter}
               AND g.id <> @SeedId
               AND (
                    (@Category IS NOT NULL AND cat.name = @Category)
                    OR EXISTS (
                        SELECT 1 FROM game_tags gt
                        WHERE gt.game_id = g.id
                          AND LOWER(gt.tag) = ANY(@Tags)
                    )
               )
             {ExcludeClause(excludeIds)}
             ORDER BY g.published_at DESC NULLS LAST
             LIMIT @Limit;
             """,
            new
            {
                SeedId = seed.Id,
                Category = seed.Category,
                Tags = seed.Tags.Select(t => t.ToLowerInvariant()).ToArray(),
                Limit = limit,
                ExcludeIds = excludeIds?.ToArray() ?? []
            },
            "similar",
            cancellationToken);

    private async Task<CandidateGame?> QuerySingleAsync(string sql, object param, CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CandidateRow>(
            new CommandDefinition(sql, param, cancellationToken: cancellationToken));
        return row?.ToCandidate("catalog");
    }

    private async Task<IReadOnlyList<CandidateGame>> QueryListAsync(
        string sql,
        object param,
        string bucket,
        CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CandidateRow>(
            new CommandDefinition(sql, param, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToCandidate(bucket)).ToList();
    }

    private static string ExcludeClause(IEnumerable<Guid>? excludeIds)
        => excludeIds is null || !excludeIds.Any()
            ? string.Empty
            : " AND g.id <> ALL(@ExcludeIds)";

    private static IReadOnlyDictionary<string, double> Aggregate(IEnumerable<(string Key, double Weight)> items)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, weight) in items)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            map[key] = map.TryGetValue(key, out var existing) ? existing + weight : weight;
        }

        if (map.Count == 0) return map;
        var max = map.Values.Max();
        if (max <= 0) return map;
        return map.ToDictionary(kv => kv.Key, kv => Math.Clamp(kv.Value / max, 0, 1), StringComparer.OrdinalIgnoreCase);
    }

    private static double Weight(string kind) => kind switch
    {
        "favorite" => 1.0,
        "completed" => 0.9,
        "played" => 0.7,
        "viewed" => 0.3,
        "clicked" => 0.2,
        _ => 0.4
    };

    private static IReadOnlyList<string> SplitTags(string? joined)
        => string.IsNullOrWhiteSpace(joined)
            ? []
            : joined.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private sealed class SignalRow
    {
        public Guid GameId { get; init; }
        public DateTimeOffset At { get; init; }
        public int DurationSeconds { get; init; }
        public string? Category { get; init; }
        public string? TagsJoined { get; init; }
    }

    private sealed class CandidateRow
    {
        public Guid Id { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? ThumbnailUrl { get; init; }
        public string? Category { get; init; }
        public string? Orientation { get; init; }
        public string? Platform { get; init; }
        public bool MobileReady { get; init; }
        public bool Multiplayer { get; init; }
        public DateTimeOffset? PublishedAt { get; init; }
        public double PopularityProxy { get; init; }
        public double EngagementProxy { get; init; }
        public int GlobalPlaySessions { get; init; }
        public double DiscoveryScore { get; init; }
        public double TrendingScore { get; init; }
        public double FreshnessScore { get; init; }
        public double MomentumScore { get; init; }
        public string? TagsJoined { get; init; }

        public CandidateGame ToCandidate(string bucket) => new()
        {
            Id = Id,
            Slug = Slug,
            Title = Title,
            Description = Description,
            ThumbnailUrl = ThumbnailUrl,
            Category = Category,
            Orientation = Orientation,
            Platform = Platform,
            MobileReady = MobileReady,
            Multiplayer = Multiplayer,
            PublishedAt = PublishedAt,
            PopularityProxy = PopularityProxy,
            EngagementProxy = EngagementProxy,
            GlobalPlaySessions = GlobalPlaySessions,
            DiscoveryScore = DiscoveryScore,
            TrendingScore = TrendingScore,
            FreshnessScore = FreshnessScore,
            MomentumScore = MomentumScore,
            Tags = SplitTags(TagsJoined),
            SourceBucket = bucket
        };
    }
}

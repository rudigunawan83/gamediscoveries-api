using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Ranking;
using GameDiscoveries.Modules.Catalog.Features.ListGames;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Discovery.Features.GetHomeDiscoveries;

public sealed class GetHomeDiscoveriesHandler(
    IDbConnectionFactory connectionFactory,
    ICacheService cache,
    IGameRankingService rankingService,
    IEnumerable<IDiscoveryRankingProvider> discoveryRankingProviders,
    ILogger<GetHomeDiscoveriesHandler> logger)
{
    private const string CacheKey = "discoveries:home:v3";
    private const int ShelfSize = 12;
    private const string FeedOrder = "m.position ASC NULLS LAST, g.published_at DESC NULLS LAST, g.title ASC";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<HomeDiscoveriesResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync<HomeDiscoveriesResponse>(CacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        var featured = await QueryByFeedAsync(connection, "Featured", FeedOrder, ShelfSize, cancellationToken);
        var latest = await QueryByFeedAsync(connection, "Latest", FeedOrder, ShelfSize, cancellationToken);
        var multiplayer = await QueryByFeedAsync(connection, "Multiplayer", FeedOrder, ShelfSize, cancellationToken);
        var mobile = await QueryByFeedAsync(connection, "Mobile", FeedOrder, ShelfSize, cancellationToken);
        var hotGames = await QueryByFeedAsync(connection, "HotGames", FeedOrder, ShelfSize, cancellationToken);
        var bestGames = await QueryByFeedAsync(connection, "BestGames", FeedOrder, ShelfSize, cancellationToken);
        var mostPlayed = await QueryByFeedAsync(connection, "MostPlayed", FeedOrder, ShelfSize, cancellationToken);
        var exclusiveGames = await QueryByFeedAsync(connection, "ExclusiveGames", FeedOrder, ShelfSize, cancellationToken);
        var popular = await QueryPopularOnSiteAsync(connection, ShelfSize, cancellationToken);
        var trending = await QueryDiscoveryTrendingAsync(connection, ShelfSize, cancellationToken);
        if (trending.Count == 0)
        {
            trending = await QueryTrendingAsync(connection, rankingService.OrderBySqlClause(GameRankingKind.Trending), ShelfSize, cancellationToken);
        }

        if (latest.Count == 0)
        {
            latest = await QueryPublishedAsync(connection, rankingService.OrderBySqlClause(GameRankingKind.Newest), ShelfSize, cancellationToken);
        }

        if (mobile.Count == 0)
        {
            mobile = await QueryPortraitAsync(connection, ShelfSize, cancellationToken);
        }

        if (popular.Count < ShelfSize)
        {
            var mostPlayedFeed = await QueryByFeedAsync(connection, "MostPlayed", FeedOrder, ShelfSize * 2, cancellationToken);
            popular = HomeShelves.TopUp(popular, mostPlayedFeed.Skip(mostPlayed.Count), ShelfSize);
        }

        if (featured.Count == 0)
        {
            featured = latest.Take(8).ToList();
        }

        if (exclusiveGames.Count == 0)
        {
            exclusiveGames = await QueryExclusiveFallbackAsync(connection, 12, cancellationToken);
            if (exclusiveGames.Count == 0)
            {
                exclusiveGames = featured.Count > 0 ? featured : bestGames;
            }
        }

        var response = new HomeDiscoveriesResponse(
            featured,
            trending,
            latest,
            popular,
            mobile,
            multiplayer,
            hotGames,
            bestGames,
            mostPlayed,
            exclusiveGames);

        await cache.SetAsync(CacheKey, response, CacheTtl, cancellationToken);
        logger.LogInformation("Built home discoveries payload");
        return response;
    }

    private static async Task<IReadOnlyList<GameSummaryResponse>> QueryByFeedAsync(
        DbConnection connection,
        string feedType,
        string orderBy,
        int limit,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                g.id AS Id,
                g.slug AS Slug,
                g.title AS Title,
                g.description AS Description,
                g.thumbnail_url AS ThumbnailUrl,
                g.cover_url AS CoverUrl,
                g.game_url AS GameUrl,
                cat.name AS Category,
                g.platform AS Platform,
                g.mobile_ready AS MobileReady,
                g.published_at AS PublishedAt
            FROM game_feed_memberships m
            INNER JOIN games g ON g.id = m.game_id
            LEFT JOIN LATERAL (
                SELECT c.name
                FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id
                ORDER BY c.name
                LIMIT 1
            ) cat ON TRUE
            WHERE m.feed_type = @FeedType
              AND g.status = 'published'
            ORDER BY {orderBy}
            LIMIT @Limit;
            """;

        var rows = await connection.QueryAsync<GameSummaryRow>(
            new CommandDefinition(sql, new { FeedType = feedType, Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(row => row.ToResponse()).ToList();
    }

    private static async Task<IReadOnlyList<GameSummaryResponse>> QueryPortraitAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                g.id AS Id,
                g.slug AS Slug,
                g.title AS Title,
                g.description AS Description,
                g.thumbnail_url AS ThumbnailUrl,
                g.cover_url AS CoverUrl,
                g.game_url AS GameUrl,
                cat.name AS Category,
                g.platform AS Platform,
                g.mobile_ready AS MobileReady,
                g.published_at AS PublishedAt
            FROM games g
            LEFT JOIN game_feed_memberships m
                ON m.game_id = g.id AND m.feed_type = 'Latest'
            LEFT JOIN LATERAL (
                SELECT c.name
                FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id
                ORDER BY c.name
                LIMIT 1
            ) cat ON TRUE
            WHERE g.status = 'published'
              AND g.mobile_ready
              AND g.orientation = 'portrait'
            ORDER BY m.position ASC NULLS LAST, g.published_at DESC NULLS LAST, g.title ASC
            LIMIT @Limit;
            """;

        var rows = await connection.QueryAsync<GameSummaryRow>(
            new CommandDefinition(sql, new { Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(row => row.ToResponse()).ToList();
    }

    private static async Task<IReadOnlyList<GameSummaryResponse>> QueryPopularOnSiteAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH plays AS (
                SELECT
                    s.game_id,
                    COUNT(DISTINCT COALESCE(s.user_id::text, s.anonymous_id::text)) AS players,
                    COUNT(*) AS sessions,
                    SUM(s.active_seconds) AS active_seconds
                FROM game_play_sessions s
                WHERE s.started_at >= NOW() - INTERVAL '30 days'
                  AND s.status <> 'INVALID'
                GROUP BY s.game_id
            )
            SELECT
                g.id AS Id,
                g.slug AS Slug,
                g.title AS Title,
                g.description AS Description,
                g.thumbnail_url AS ThumbnailUrl,
                g.cover_url AS CoverUrl,
                g.game_url AS GameUrl,
                cat.name AS Category,
                g.platform AS Platform,
                g.mobile_ready AS MobileReady,
                g.published_at AS PublishedAt
            FROM plays p
            INNER JOIN games g ON g.id = p.game_id
            LEFT JOIN LATERAL (
                SELECT c.name
                FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id
                ORDER BY c.name
                LIMIT 1
            ) cat ON TRUE
            WHERE g.status = 'published'
            ORDER BY p.players DESC, p.sessions DESC, p.active_seconds DESC, g.title ASC
            LIMIT @Limit;
            """;

        var rows = await connection.QueryAsync<GameSummaryRow>(
            new CommandDefinition(sql, new { Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(row => row.ToResponse()).ToList();
    }

    private static async Task<IReadOnlyList<GameSummaryResponse>> QueryExclusiveFallbackAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                g.id AS Id,
                g.slug AS Slug,
                g.title AS Title,
                g.description AS Description,
                g.thumbnail_url AS ThumbnailUrl,
                g.cover_url AS CoverUrl,
                g.game_url AS GameUrl,
                cat.name AS Category,
                g.platform AS Platform,
                g.mobile_ready AS MobileReady,
                g.published_at AS PublishedAt
            FROM games g
            LEFT JOIN LATERAL (
                SELECT c.name
                FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id
                ORDER BY c.name
                LIMIT 1
            ) cat ON TRUE
            WHERE g.status = 'published'
              AND (
                LOWER(g.title) LIKE '%zuma%'
                OR EXISTS (
                    SELECT 1
                    FROM game_tag_links gtl
                    INNER JOIN tags t ON t.id = gtl.tag_id
                    WHERE gtl.game_id = g.id
                      AND (
                        LOWER(t.slug) IN ('zuma', 'match-3', 'match3', 'bubble-shooter', 'exclusive')
                        OR LOWER(t.name) LIKE '%zuma%'
                        OR LOWER(t.name) LIKE '%match%3%'
                      )
                )
              )
            ORDER BY g.published_at DESC NULLS LAST, g.title ASC
            LIMIT @Limit;
            """;

        var rows = await connection.QueryAsync<GameSummaryRow>(
            new CommandDefinition(sql, new { Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(row => row.ToResponse()).ToList();
    }

    private async Task<IReadOnlyList<GameSummaryResponse>> QueryDiscoveryTrendingAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        foreach (var provider in discoveryRankingProviders)
        {
            var ids = await provider.GetTrendingGameIdsAsync(limit, cancellationToken);
            if (ids.Count == 0)
            {
                continue;
            }

            var rows = await connection.QueryAsync<GameSummaryRow>(new CommandDefinition(
                """
                SELECT
                    g.id AS Id,
                    g.slug AS Slug,
                    g.title AS Title,
                    g.description AS Description,
                    g.thumbnail_url AS ThumbnailUrl,
                    g.cover_url AS CoverUrl,
                    g.game_url AS GameUrl,
                    cat.name AS Category,
                    g.platform AS Platform,
                    g.mobile_ready AS MobileReady,
                    g.published_at AS PublishedAt
                FROM games g
                LEFT JOIN LATERAL (
                    SELECT c.name
                    FROM game_categories gc
                    INNER JOIN categories c ON c.id = gc.category_id
                    WHERE gc.game_id = g.id
                    ORDER BY c.name
                    LIMIT 1
                ) cat ON TRUE
                WHERE g.status = 'published'
                  AND g.id = ANY(@Ids)
                """,
                new { Ids = ids.ToArray() },
                cancellationToken: cancellationToken));

            var map = rows.ToDictionary(r => r.Id);
            var ordered = ids
                .Where(map.ContainsKey)
                .Select(id => map[id].ToResponse())
                .ToList();
            if (ordered.Count > 0)
            {
                return ordered;
            }
        }

        return [];
    }

    private static async Task<IReadOnlyList<GameSummaryResponse>> QueryTrendingAsync(
        DbConnection connection,
        string orderBy,
        int limit,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                g.id AS Id,
                g.slug AS Slug,
                g.title AS Title,
                g.description AS Description,
                g.thumbnail_url AS ThumbnailUrl,
                g.cover_url AS CoverUrl,
                g.game_url AS GameUrl,
                cat.name AS Category,
                g.platform AS Platform,
                g.mobile_ready AS MobileReady,
                g.published_at AS PublishedAt
            FROM games g
            LEFT JOIN LATERAL (
                SELECT c.name
                FROM game_categories gc
                INNER JOIN categories c ON c.id = gc.category_id
                WHERE gc.game_id = g.id
                ORDER BY c.name
                LIMIT 1
            ) cat ON TRUE
            WHERE g.status = 'published'
            ORDER BY {orderBy}
            LIMIT @Limit;
            """;

        var rows = await connection.QueryAsync<GameSummaryRow>(
            new CommandDefinition(sql, new { Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(row => row.ToResponse()).ToList();
    }

    private static Task<IReadOnlyList<GameSummaryResponse>> QueryPublishedAsync(
        DbConnection connection,
        string orderBy,
        int limit,
        CancellationToken cancellationToken)
        => QueryTrendingAsync(connection, orderBy, limit, cancellationToken);
}

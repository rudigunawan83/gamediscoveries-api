using System.Data.Common;
using Dapper;
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
    ILogger<GetHomeDiscoveriesHandler> logger)
{
    private const string CacheKey = "discoveries:home:v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<HomeDiscoveriesResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync<HomeDiscoveriesResponse>(CacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        var featured = await QueryByFeedAsync(connection, "Featured", rankingService.OrderBySqlClause(GameRankingKind.Featured), 12, cancellationToken);
        var popular = await QueryByFeedAsync(connection, "Popular", rankingService.OrderBySqlClause(GameRankingKind.Popular), 12, cancellationToken);
        var latest = await QueryByFeedAsync(connection, "Latest", rankingService.OrderBySqlClause(GameRankingKind.Newest), 12, cancellationToken);
        var multiplayer = await QueryByFeedAsync(connection, "Multiplayer", rankingService.OrderBySqlClause(GameRankingKind.Popular), 12, cancellationToken);
        var mobile = await QueryByFeedAsync(connection, "Mobile", rankingService.OrderBySqlClause(GameRankingKind.Newest), 12, cancellationToken);
        var trending = await QueryTrendingAsync(connection, rankingService.OrderBySqlClause(GameRankingKind.Trending), 12, cancellationToken);

        // Fallbacks when feed memberships are empty (pre-sync).
        if (latest.Count == 0)
        {
            latest = await QueryPublishedAsync(connection, rankingService.OrderBySqlClause(GameRankingKind.Newest), 12, cancellationToken);
        }

        if (trending.Count == 0)
        {
            trending = popular.Count > 0
                ? popular
                : await QueryPublishedAsync(connection, rankingService.OrderBySqlClause(GameRankingKind.Trending), 12, cancellationToken);
        }

        if (featured.Count == 0)
        {
            featured = latest.Take(8).ToList();
        }

        var response = new HomeDiscoveriesResponse(
            featured,
            trending,
            latest,
            popular.Count > 0 ? popular : latest,
            mobile.Count > 0 ? mobile : latest.Where(g => g.MobileReady).Take(12).ToList(),
            multiplayer);

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

    private static async Task<IReadOnlyList<GameSummaryResponse>> QueryTrendingAsync(
        DbConnection connection,
        string orderBy,
        int limit,
        CancellationToken cancellationToken)
    {
        // Intentionally NOT equal to Popular feed membership.
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

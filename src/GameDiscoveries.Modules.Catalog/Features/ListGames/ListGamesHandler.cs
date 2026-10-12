using System.Data.Common;
using System.Text;
using Dapper;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Pagination;
using GameDiscoveries.BuildingBlocks.Ranking;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Catalog.Features.ListGames;

public sealed class ListGamesHandler(
    IDbConnectionFactory connectionFactory,
    IGameRankingService rankingService,
    ISearchService search,
    ILogger<ListGamesHandler> logger)
{
    private const string GamesSearchIndex = "games";
    private const int MaxSearchHits = 200;

    public async Task<(IReadOnlyList<GameSummaryResponse> Items, PaginationMeta Meta)> HandleAsync(
        ListGamesQuery query,
        CancellationToken cancellationToken = default)
    {
        var filters = new StringBuilder("""
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
            """);

        var parameters = new DynamicParameters();
        parameters.Add("Offset", query.Skip);
        parameters.Add("Limit", query.Take);

        Guid[] rankedIds = [];
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rankedIds = await SearchIdsAsync(term, cancellationToken);
            // Meilisearch adds typo-tolerant, ranked matches; the substring match keeps results
            // correct when the index lags the catalog or the search service is down.
            filters.Append(" AND (g.id = ANY(@SearchIds) OR g.title ILIKE @Search OR g.description ILIKE @Search)");
            parameters.Add("Search", $"%{term}%");
            parameters.Add("SearchIds", rankedIds);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filters.Append("""
                 AND EXISTS (
                    SELECT 1
                    FROM game_categories gc2
                    INNER JOIN categories c2 ON c2.id = gc2.category_id
                    WHERE gc2.game_id = g.id
                      AND (c2.slug = @Category OR LOWER(c2.name) = LOWER(@Category))
                )
                """);
            parameters.Add("Category", query.Category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Platform))
        {
            filters.Append(" AND LOWER(g.platform) = LOWER(@Platform)");
            parameters.Add("Platform", query.Platform.Trim());
        }

        if (query.MobileReady is not null)
        {
            filters.Append(" AND g.mobile_ready = @MobileReady");
            parameters.Add("MobileReady", query.MobileReady.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Orientation))
        {
            filters.Append(" AND g.orientation = @Orientation");
            parameters.Add("Orientation", query.Orientation.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            filters.Append("""
                 AND (
                    EXISTS (
                        SELECT 1 FROM game_tags gt
                        WHERE gt.game_id = g.id AND LOWER(gt.tag) = LOWER(@Tag)
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM game_tag_links gtl
                        INNER JOIN tags t ON t.id = gtl.tag_id
                        WHERE gtl.game_id = g.id
                          AND (t.slug = @Tag OR LOWER(t.name) = LOWER(@Tag))
                    )
                )
                """);
            parameters.Add("Tag", query.Tag.Trim());
        }

        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "title" => "g.title ASC",
            "trending" => rankingService.OrderBySqlClause(GameRankingKind.Trending),
            "popular" => rankingService.OrderBySqlClause(GameRankingKind.Popular),
            _ => rankingService.OrderBySqlClause(GameRankingKind.Newest)
        };
        if (rankedIds.Length > 0 && IsRelevanceSort(query.Sort))
        {
            orderBy = $"array_position(@SearchIds, g.id) NULLS LAST, {orderBy}";
        }

        var countSql = "SELECT COUNT(*) " + filters;
        var listSql = $"""
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
            {filters}
            ORDER BY {orderBy}
            OFFSET @Offset LIMIT @Limit;
            """;

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var total = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<GameSummaryRow>(
            new CommandDefinition(listSql, parameters, cancellationToken: cancellationToken)))
            .Select(row => row.ToResponse())
            .ToList();

        logger.LogInformation(
            "Listed games page={Page} size={PageSize} total={Total}",
            query.Page,
            query.Take,
            total);

        return (items, PaginationMeta.Create(query.Page, query.Take, total));
    }

    /// <summary>Search ranks results unless the caller asked for an explicit order.</summary>
    public static bool IsRelevanceSort(string? sort) =>
        string.IsNullOrWhiteSpace(sort) || sort.Trim().Equals("relevance", StringComparison.OrdinalIgnoreCase);

    private async Task<Guid[]> SearchIdsAsync(string term, CancellationToken cancellationToken)
    {
        try
        {
            var result = await search.SearchAsync<SearchHit>(GamesSearchIndex, term, MaxSearchHits, cancellationToken);
            return result.Hits
                .Select(hit => Guid.TryParse(hit.Id, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Search index unavailable; falling back to substring match");
            return [];
        }
    }

    private sealed class SearchHit
    {
        public string Id { get; init; } = string.Empty;
    }
}

using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Catalog.Features.ListCategories;

public sealed class ListCategoriesHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<ListCategoriesHandler> logger)
{
    public async Task<IReadOnlyList<CategoryResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                c.id AS Id,
                c.slug AS Slug,
                c.name AS Name,
                c.description AS Description,
                COUNT(g.id)::int AS GameCount,
                MAX(COALESCE(g.updated_at, g.published_at, g.created_at)) AS LastContentAt
            FROM categories c
            INNER JOIN game_categories gc ON gc.category_id = c.id
            INNER JOIN games g ON g.id = gc.game_id AND g.status = 'published'
            GROUP BY c.id, c.slug, c.name, c.description
            ORDER BY COUNT(g.id) DESC, c.name ASC
            """;

        var rows = (await connection.QueryAsync<CategoryResponse>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).AsList();

        logger.LogDebug("Listed {Count} categories with published games", rows.Count);
        return rows;
    }
}

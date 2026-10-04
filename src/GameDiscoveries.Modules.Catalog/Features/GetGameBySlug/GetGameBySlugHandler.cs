using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

public sealed class GetGameBySlugHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<GetGameBySlugHandler> logger)
{
    public async Task<GameResponse> HandleAsync(
        GetGameBySlugQuery query,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                g.id AS Id,
                g.slug AS Slug,
                g.title AS Title,
                g.description AS Description,
                COALESCE(g.instructions, m.raw_payload ->> 'instructions') AS Instructions,
                g.thumbnail_url AS ThumbnailUrl,
                g.cover_url AS CoverUrl,
                g.game_url AS GameUrl,
                g.embed_url AS EmbedUrl,
                cat.name AS Category,
                g.developer AS Developer,
                g.platform AS Platform,
                g.status AS Status,
                g.mobile_ready AS MobileReady,
                g.orientation AS Orientation,
                g.width AS Width,
                g.height AS Height,
                g.created_at AS CreatedAt,
                g.updated_at AS UpdatedAt,
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
            LEFT JOIN LATERAL (
                SELECT raw_payload
                FROM game_provider_mappings gpm
                WHERE gpm.game_id = g.id
                ORDER BY gpm.last_synced_at DESC NULLS LAST
                LIMIT 1
            ) m ON TRUE
            WHERE g.slug = @Slug
            LIMIT 1;
            """;

        const string tagsSql = """
            SELECT DISTINCT tag
            FROM (
                SELECT gt.tag AS tag
                FROM game_tags gt
                WHERE gt.game_id = @GameId
                UNION ALL
                SELECT t.name AS tag
                FROM game_tag_links gtl
                INNER JOIN tags t ON t.id = gtl.tag_id
                WHERE gtl.game_id = @GameId
            ) tags
            ORDER BY tag;
            """;

        await using var connection = (System.Data.Common.DbConnection)
            await connectionFactory.CreateConnectionAsync(cancellationToken);

        var game = await connection.QuerySingleOrDefaultAsync<GameRow>(
            new CommandDefinition(sql, new { query.Slug }, cancellationToken: cancellationToken));

        if (game is null)
        {
            throw new NotFoundException(
                "Game Not Found",
                $"The requested game '{query.Slug}' could not be found.",
                "https://api.gamediscoveries.com/errors/game-not-found");
        }

        var tags = (await connection.QueryAsync<string>(
            new CommandDefinition(tagsSql, new { GameId = game.Id }, cancellationToken: cancellationToken)))
            .ToList();

        logger.LogInformation("Game retrieved {GameId} {Slug}", game.Id, game.Slug);

        return GameMapper.ToResponse(game, tags);
    }
}

public sealed class GameRow
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? CoverUrl { get; init; }
    public string? GameUrl { get; init; }
    public string? EmbedUrl { get; init; }
    public string? Category { get; init; }
    public string? Developer { get; init; }
    public string? Platform { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool MobileReady { get; init; }
    public string? Orientation { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
}

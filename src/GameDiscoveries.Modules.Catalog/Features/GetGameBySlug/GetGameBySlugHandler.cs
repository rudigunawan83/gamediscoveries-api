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
            WHERE g.slug = @Slug
            LIMIT 1;
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

        logger.LogInformation("Game retrieved {GameId} {Slug}", game.Id, game.Slug);

        return GameMapper.ToResponse(game);
    }
}

public sealed class GameRow
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
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

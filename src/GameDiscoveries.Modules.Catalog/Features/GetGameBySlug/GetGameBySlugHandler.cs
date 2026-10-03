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
                id AS Id,
                slug AS Slug,
                title AS Title,
                description AS Description,
                thumbnail_url AS ThumbnailUrl,
                cover_url AS CoverUrl,
                game_url AS GameUrl,
                status AS Status,
                mobile_ready AS MobileReady,
                orientation AS Orientation,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                published_at AS PublishedAt
            FROM games
            WHERE slug = @Slug
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
    public string Status { get; init; } = string.Empty;
    public bool MobileReady { get; init; }
    public string? Orientation { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
}

using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Catalog.Features.ListGames;
using GameDiscoveries.Modules.Favorites.Models;

namespace GameDiscoveries.Modules.Favorites.Data;

public interface IUserLibraryRepository
{
    Task<bool> GameExistsPublishedAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FavoriteItemResponse> Items, long Total)> ListFavoritesAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> IsFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    Task<bool> AddFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<HistoryItemResponse> Items, long Total)> ListHistoryAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task UpsertHistoryAsync(Guid userId, Guid gameId, int durationSeconds, CancellationToken cancellationToken = default);
}

public sealed class UserLibraryRepository(IDbConnectionFactory connectionFactory) : IUserLibraryRepository
{
    private const string GameSelect = """
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
        """;

    private const string CategoryJoin = """
        LEFT JOIN LATERAL (
            SELECT c.name
            FROM game_categories gc
            INNER JOIN categories c ON c.id = gc.category_id
            WHERE gc.game_id = g.id
            ORDER BY c.name
            LIMIT 1
        ) cat ON TRUE
        """;

    public async Task<bool> GameExistsPublishedAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM games
                WHERE id = @GameId AND status = 'published'
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { GameId = gameId }, cancellationToken: cancellationToken));
    }

    public async Task<(IReadOnlyList<FavoriteItemResponse> Items, long Total)> ListFavoritesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string countSql = "SELECT COUNT(*) FROM user_favorites WHERE user_id = @UserId;";
        var total = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, new { UserId = userId }, cancellationToken: cancellationToken));

        var offset = Math.Max(page - 1, 0) * pageSize;
        var sql = $"""
            SELECT
                f.game_id AS GameId,
                f.created_at AS FavoritedAt,
                {GameSelect}
            FROM user_favorites f
            INNER JOIN games g ON g.id = f.game_id
            {CategoryJoin}
            WHERE f.user_id = @UserId
              AND g.status = 'published'
            ORDER BY f.created_at DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        var rows = await connection.QueryAsync<FavoriteRow>(
            new CommandDefinition(sql, new { UserId = userId, Limit = pageSize, Offset = offset }, cancellationToken: cancellationToken));

        return (rows.Select(r => r.ToResponse()).ToList(), total);
    }

    public async Task<bool> IsFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM user_favorites
                WHERE user_id = @UserId AND game_id = @GameId
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { UserId = userId, GameId = gameId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> AddFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            INSERT INTO user_favorites (user_id, game_id, created_at)
            VALUES (@UserId, @GameId, (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (user_id, game_id) DO NOTHING;
            """;
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, GameId = gameId }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> RemoveFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            DELETE FROM user_favorites
            WHERE user_id = @UserId AND game_id = @GameId;
            """;
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, GameId = gameId }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<(IReadOnlyList<HistoryItemResponse> Items, long Total)> ListHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string countSql = "SELECT COUNT(*) FROM user_play_history WHERE user_id = @UserId;";
        var total = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, new { UserId = userId }, cancellationToken: cancellationToken));

        var offset = Math.Max(page - 1, 0) * pageSize;
        var sql = $"""
            SELECT
                h.id AS Id,
                h.game_id AS GameId,
                h.played_at AS PlayedAt,
                h.duration_seconds AS DurationSeconds,
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
            FROM user_play_history h
            INNER JOIN games g ON g.id = h.game_id
            {CategoryJoin}
            WHERE h.user_id = @UserId
              AND g.status = 'published'
            ORDER BY h.played_at DESC
            LIMIT @Limit OFFSET @Offset;
            """;

        var rows = await connection.QueryAsync<HistoryRow>(
            new CommandDefinition(sql, new { UserId = userId, Limit = pageSize, Offset = offset }, cancellationToken: cancellationToken));

        return (rows.Select(r => r.ToResponse()).ToList(), total);
    }

    public async Task UpsertHistoryAsync(Guid userId, Guid gameId, int durationSeconds, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            INSERT INTO user_play_history (id, user_id, game_id, played_at, duration_seconds)
            VALUES (@Id, @UserId, @GameId, (NOW() AT TIME ZONE 'utc'), @DurationSeconds)
            ON CONFLICT (user_id, game_id) DO UPDATE
            SET played_at = EXCLUDED.played_at,
                duration_seconds = GREATEST(user_play_history.duration_seconds, EXCLUDED.duration_seconds);
            """;
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    GameId = gameId,
                    DurationSeconds = Math.Max(0, durationSeconds)
                },
                cancellationToken: cancellationToken));
    }

    private sealed class FavoriteRow
    {
        public Guid GameId { get; init; }
        public DateTimeOffset FavoritedAt { get; init; }
        public Guid Id { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? ThumbnailUrl { get; init; }
        public string? CoverUrl { get; init; }
        public string? GameUrl { get; init; }
        public string? Category { get; init; }
        public string? Platform { get; init; }
        public bool MobileReady { get; init; }
        public DateTimeOffset? PublishedAt { get; init; }

        public FavoriteItemResponse ToResponse() => new(
            GameId,
            FavoritedAt,
            new GameSummaryResponse(
                Id, Slug, Title, Description, ThumbnailUrl, CoverUrl, GameUrl,
                Category, Platform, MobileReady, PublishedAt));
    }

    private sealed class HistoryRow
    {
        public Guid Id { get; init; }
        public Guid GameId { get; init; }
        public DateTimeOffset PlayedAt { get; init; }
        public int DurationSeconds { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? ThumbnailUrl { get; init; }
        public string? CoverUrl { get; init; }
        public string? GameUrl { get; init; }
        public string? Category { get; init; }
        public string? Platform { get; init; }
        public bool MobileReady { get; init; }
        public DateTimeOffset? PublishedAt { get; init; }

        public HistoryItemResponse ToResponse() => new(
            Id,
            GameId,
            PlayedAt,
            DurationSeconds,
            new GameSummaryResponse(
                GameId, Slug, Title, Description, ThumbnailUrl, CoverUrl, GameUrl,
                Category, Platform, MobileReady, PublishedAt));
    }
}

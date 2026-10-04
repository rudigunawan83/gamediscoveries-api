using System.Data;
using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Feeds;
using GameDiscoveries.BuildingBlocks.Text;
using GameDiscoveries.Infrastructure.Providers.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public sealed class GameFeedImportService(
    GameMonetizeClient client,
    IDbConnectionFactory connectionFactory,
    ICacheService cache,
    IOptions<GameMonetizeOptions> options,
    ILogger<GameFeedImportService> logger) : IGameFeedImportService
{
    private static readonly Guid GameMonetizeProviderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public async Task<FeedImportResult> ImportAsync(
        GameFeedType feedType,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;
        var totalReceived = 0;

        if (!options.Value.Enabled)
        {
            errors.Add("GameMonetize provider is disabled.");
            return BuildResult(feedType, startedAt, totalReceived, created, updated, skipped, failed, errors);
        }

        try
        {
            var feedItems = await client.FetchFeedAsync(feedType, cancellationToken);
            totalReceived = feedItems.Count;

            await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
            await EnsureProviderAsync(connection, cancellationToken);

            foreach (var item in feedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var external = GameMonetizeMapper.Map(item);
                    if (external is null)
                    {
                        skipped++;
                        continue;
                    }

                    var outcome = await UpsertGameAsync(connection, external, feedType, cancellationToken);
                    switch (outcome)
                    {
                        case UpsertOutcome.Created:
                            created++;
                            break;
                        case UpsertOutcome.Updated:
                            updated++;
                            break;
                        default:
                            skipped++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    var message = $"Failed item '{item.Id ?? item.Title}': {ex.Message}";
                    errors.Add(message);
                    logger.LogWarning(ex, "Game feed item failed for {FeedType}", feedType);
                }
            }

            await UpdateFeedSyncStateAsync(
                connection,
                feedType,
                success: failed == 0,
                error: errors.Count == 0 ? null : string.Join("; ", errors.Take(5)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            failed++;
            errors.Add(ex.Message);
            logger.LogError(ex, "[GameFeedSync] {FeedType} feed failed", feedType);

            try
            {
                await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
                await UpdateFeedSyncStateAsync(connection, feedType, success: false, error: ex.Message, cancellationToken);
            }
            catch (Exception updateEx)
            {
                logger.LogError(updateEx, "Failed to persist feed sync failure state for {FeedType}", feedType);
            }
        }

        var result = BuildResult(feedType, startedAt, totalReceived, created, updated, skipped, failed, errors);
        logger.LogInformation(
            "[GameFeedSync] {FeedType} feed completed. Received={Received} Created={Created} Updated={Updated} Skipped={Skipped} Failed={Failed} Duration={Duration}ms",
            feedType,
            result.TotalReceived,
            result.Created,
            result.Updated,
            result.Skipped,
            result.Failed,
            result.DurationMs);

        if (created > 0 || updated > 0)
        {
            await cache.RemoveAsync("discoveries:home:v1", cancellationToken);
        }

        return result;
    }

    public async Task<IReadOnlyList<FeedImportResult>> ImportAllAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<FeedImportResult>();
        foreach (var feedType in Enum.GetValues<GameFeedType>())
        {
            results.Add(await ImportAsync(feedType, cancellationToken));
        }

        return results;
    }

    private async Task<UpsertOutcome> UpsertGameAsync(
        DbConnection connection,
        ExternalGame external,
        GameFeedType feedType,
        CancellationToken cancellationToken)
    {
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await connection.QuerySingleOrDefaultAsync<ExistingMappingRow>(
            new CommandDefinition(
                """
                SELECT g.id AS GameId, g.slug AS Slug, g.title AS Title
                FROM game_provider_mappings m
                INNER JOIN games g ON g.id = m.game_id
                WHERE m.provider_id = @ProviderId AND m.provider_game_id = @ProviderGameId
                LIMIT 1;
                """,
                new { ProviderId = GameMonetizeProviderId, external.ProviderGameId },
                transaction: tx,
                cancellationToken: cancellationToken));

        Guid gameId;
        UpsertOutcome outcome;

        if (existing is null)
        {
            gameId = Guid.NewGuid();
            var slug = await CreateUniqueSlugAsync(connection, tx, external.Title, gameId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO games (
                        id, slug, title, description, thumbnail_url, cover_url, game_url,
                        embed_url, developer, width, height, platform, status, mobile_ready,
                        orientation, created_at, updated_at, published_at)
                    VALUES (
                        @Id, @Slug, @Title, @Description, @ThumbnailUrl, @CoverUrl, @GameUrl,
                        @EmbedUrl, @Developer, @Width, @Height, @Platform, @Status, @MobileReady,
                        @Orientation, @Now, @Now, @Now);
                    """,
                    new
                    {
                        Id = gameId,
                        Slug = slug,
                        external.Title,
                        external.Description,
                        external.ThumbnailUrl,
                        external.CoverUrl,
                        external.GameUrl,
                        external.EmbedUrl,
                        external.Developer,
                        external.Width,
                        external.Height,
                        external.Platform,
                        Status = "published",
                        external.MobileReady,
                        external.Orientation,
                        Now = now
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO game_provider_mappings (
                        id, game_id, provider_id, provider_game_id, provider_url,
                        raw_payload, last_synced_at, created_at, updated_at)
                    VALUES (
                        @Id, @GameId, @ProviderId, @ProviderGameId, @ProviderUrl,
                        CAST(@RawPayload AS jsonb), @Now, @Now, @Now);
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        GameId = gameId,
                        ProviderId = GameMonetizeProviderId,
                        external.ProviderGameId,
                        external.ProviderUrl,
                        external.RawPayload,
                        Now = now
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            outcome = UpsertOutcome.Created;
        }
        else
        {
            gameId = existing.GameId;
            var now = DateTimeOffset.UtcNow;

            // Preserve slug and internal analytics; refresh provider-owned catalog fields only.
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE games
                    SET
                        title = @Title,
                        description = @Description,
                        thumbnail_url = @ThumbnailUrl,
                        cover_url = @CoverUrl,
                        game_url = @GameUrl,
                        embed_url = @EmbedUrl,
                        developer = @Developer,
                        width = @Width,
                        height = @Height,
                        platform = @Platform,
                        mobile_ready = @MobileReady,
                        orientation = @Orientation,
                        updated_at = @Now
                    WHERE id = @GameId;
                    """,
                    new
                    {
                        GameId = gameId,
                        external.Title,
                        external.Description,
                        external.ThumbnailUrl,
                        external.CoverUrl,
                        external.GameUrl,
                        external.EmbedUrl,
                        external.Developer,
                        external.Width,
                        external.Height,
                        external.Platform,
                        external.MobileReady,
                        external.Orientation,
                        Now = now
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE game_provider_mappings
                    SET
                        provider_url = @ProviderUrl,
                        raw_payload = CAST(@RawPayload AS jsonb),
                        last_synced_at = @Now,
                        updated_at = @Now
                    WHERE provider_id = @ProviderId AND provider_game_id = @ProviderGameId;
                    """,
                    new
                    {
                        ProviderId = GameMonetizeProviderId,
                        external.ProviderGameId,
                        external.ProviderUrl,
                        external.RawPayload,
                        Now = now
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            outcome = UpsertOutcome.Updated;
        }

        await SyncCategoriesAsync(connection, tx, gameId, external.Categories, cancellationToken);
        await SyncTagsAsync(connection, tx, gameId, external.Tags, cancellationToken);
        await SyncFeedMembershipAsync(connection, tx, gameId, feedType, cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return outcome;
    }

    private static async Task SyncCategoriesAsync(
        DbConnection connection,
        IDbTransaction tx,
        Guid gameId,
        IReadOnlyCollection<string> categories,
        CancellationToken cancellationToken)
    {
        foreach (var categoryName in categories.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            var slug = BuildingBlocks.Text.SlugGenerator.FromTitle(categoryName);
            var categoryId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "SELECT id FROM categories WHERE slug = @Slug LIMIT 1;",
                    new { Slug = slug },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (categoryId is null)
            {
                categoryId = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO categories (id, slug, name, description, created_at, updated_at)
                        VALUES (@Id, @Slug, @Name, NULL, @Now, @Now)
                        ON CONFLICT (slug) DO NOTHING;
                        """,
                        new { Id = categoryId, Slug = slug, Name = categoryName.Trim(), Now = now },
                        transaction: tx,
                        cancellationToken: cancellationToken));

                categoryId = await connection.QuerySingleAsync<Guid>(
                    new CommandDefinition(
                        "SELECT id FROM categories WHERE slug = @Slug LIMIT 1;",
                        new { Slug = slug },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO game_categories (game_id, category_id, created_at)
                    VALUES (@GameId, @CategoryId, NOW() AT TIME ZONE 'utc')
                    ON CONFLICT DO NOTHING;
                    """,
                    new { GameId = gameId, CategoryId = categoryId },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }
    }

    private static async Task SyncTagsAsync(
        DbConnection connection,
        IDbTransaction tx,
        Guid gameId,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken)
    {
        foreach (var tagName in tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()))
        {
            var slug = BuildingBlocks.Text.SlugGenerator.FromTitle(tagName);
            var tagId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "SELECT id FROM tags WHERE slug = @Slug LIMIT 1;",
                    new { Slug = slug },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (tagId is null)
            {
                tagId = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO tags (id, slug, name, created_at, updated_at)
                        VALUES (@Id, @Slug, @Name, @Now, @Now)
                        ON CONFLICT (slug) DO NOTHING;
                        """,
                        new { Id = tagId, Slug = slug, Name = tagName, Now = now },
                        transaction: tx,
                        cancellationToken: cancellationToken));

                tagId = await connection.QuerySingleAsync<Guid>(
                    new CommandDefinition(
                        "SELECT id FROM tags WHERE slug = @Slug LIMIT 1;",
                        new { Slug = slug },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO game_tag_links (game_id, tag_id, created_at)
                    VALUES (@GameId, @TagId, NOW() AT TIME ZONE 'utc')
                    ON CONFLICT DO NOTHING;
                    """,
                    new { GameId = gameId, TagId = tagId },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO game_tags (id, game_id, tag, created_at)
                    VALUES (@Id, @GameId, @Tag, NOW() AT TIME ZONE 'utc')
                    ON CONFLICT DO NOTHING;
                    """,
                    new { Id = Guid.NewGuid(), GameId = gameId, Tag = tagName },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }
    }

    private static async Task SyncFeedMembershipAsync(
        DbConnection connection,
        IDbTransaction tx,
        Guid gameId,
        GameFeedType feedType,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO game_feed_memberships (game_id, feed_type, provider_id, synced_at)
                VALUES (@GameId, @FeedType, @ProviderId, NOW() AT TIME ZONE 'utc')
                ON CONFLICT (game_id, feed_type) DO UPDATE
                SET synced_at = EXCLUDED.synced_at;
                """,
                new
                {
                    GameId = gameId,
                    FeedType = feedType.ToString(),
                    ProviderId = GameMonetizeProviderId
                },
                transaction: tx,
                cancellationToken: cancellationToken));
    }

    private static async Task<string> CreateUniqueSlugAsync(
        DbConnection connection,
        IDbTransaction tx,
        string title,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var baseSlug = SlugGenerator.FromTitle(title);
        var slug = baseSlug;
        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM games WHERE slug = @Slug);",
                new { Slug = slug },
                transaction: tx,
                cancellationToken: cancellationToken));

        if (exists)
        {
            slug = SlugGenerator.EnsureUnique(baseSlug, gameId);
        }

        return slug;
    }

    private static async Task EnsureProviderAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO game_providers (id, name, status, created_at, updated_at)
                VALUES (@Id, @Name, 'active', NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc')
                ON CONFLICT (name) DO NOTHING;
                """,
                new { Id = GameMonetizeProviderId, Name = GameMonetizeMapper.SourceName },
                cancellationToken: cancellationToken));
    }

    private static async Task UpdateFeedSyncStateAsync(
        DbConnection connection,
        GameFeedType feedType,
        bool success,
        string? error,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE game_feeds
                SET
                    last_synced_at = NOW() AT TIME ZONE 'utc',
                    last_sync_status = @Status,
                    last_sync_error = @Error,
                    updated_at = NOW() AT TIME ZONE 'utc'
                WHERE feed_type = @FeedType;
                """,
                new
                {
                    FeedType = feedType.ToString(),
                    Status = success ? "succeeded" : "failed",
                    Error = error
                },
                cancellationToken: cancellationToken));
    }

    private static FeedImportResult BuildResult(
        GameFeedType feedType,
        DateTimeOffset startedAt,
        int totalReceived,
        int created,
        int updated,
        int skipped,
        int failed,
        IReadOnlyList<string> errors)
        => new()
        {
            FeedType = feedType,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            TotalReceived = totalReceived,
            Created = created,
            Updated = updated,
            Skipped = skipped,
            Failed = failed,
            Errors = errors
        };

    private enum UpsertOutcome
    {
        Created,
        Updated,
        Skipped
    }

    private sealed class ExistingMappingRow
    {
        public Guid GameId { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
    }
}

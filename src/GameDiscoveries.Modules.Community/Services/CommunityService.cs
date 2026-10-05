using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Community.Configuration;
using GameDiscoveries.Modules.Community.Domain;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Community.Services;

public interface ICommunityService
{
    Task<CommunityHomeDto> GetHomeAsync(Guid? viewerId, CancellationToken ct = default);
    Task<(IReadOnlyList<FeedItemDto> Items, string? NextCursor)> GetFeedAsync(Guid? viewerId, string? cursor, int limit, string sort, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityPostDto>> GetGameDiscussionsAsync(string gameSlug, string sort, Guid? viewerId, CancellationToken ct = default);
    Task<CommunityPostDto> GetPostAsync(Guid id, Guid? viewerId, CancellationToken ct = default);
    Task<CommunityPostDto> CreatePostAsync(Guid userId, string type, string title, string content, Guid? gameId, CancellationToken ct = default);
    Task<CommunityPostDto> UpdatePostAsync(Guid userId, Guid postId, string title, string content, bool isMod, CancellationToken ct = default);
    Task DeletePostAsync(Guid userId, Guid postId, bool isMod, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityCommentDto>> GetCommentsAsync(Guid postId, Guid? viewerId, CancellationToken ct = default);
    Task<CommunityCommentDto> CreateCommentAsync(Guid userId, Guid postId, string content, Guid? parentId, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid userId, Guid commentId, bool isMod, CancellationToken ct = default);
    Task SetReactionAsync(Guid userId, string targetType, Guid targetId, string reaction, CancellationToken ct = default);
    Task RemoveReactionAsync(Guid userId, string targetType, Guid targetId, CancellationToken ct = default);
    Task<(GameReviewSummaryDto Summary, IReadOnlyList<GameReviewDto> Items)> GetReviewsAsync(string gameSlug, CancellationToken ct = default);
    Task<GameReviewDto> UpsertReviewAsync(Guid userId, string gameSlug, int rating, string content, CancellationToken ct = default);
    Task DeleteReviewAsync(Guid userId, Guid reviewId, bool isMod, CancellationToken ct = default);
    Task<UserProfileDto> GetProfileAsync(string username, Guid? viewerId, CancellationToken ct = default);
    Task UpdatePrivacyAsync(Guid userId, bool? showFavorites, bool? showHistory, bool? showAchievements, bool? showActivity, bool? showOnLeaderboards, string? bio, CancellationToken ct = default);
    Task FollowAsync(Guid userId, Guid targetId, CancellationToken ct = default);
    Task UnfollowAsync(Guid userId, Guid targetId, CancellationToken ct = default);
    Task BlockAsync(Guid userId, Guid targetId, CancellationToken ct = default);
    Task UnblockAsync(Guid userId, Guid targetId, CancellationToken ct = default);
    Task<IReadOnlyList<AchievementDto>> GetAchievementsAsync(Guid? userId, CancellationToken ct = default);
    Task<IReadOnlyList<ChallengeDto>> GetChallengesAsync(Guid? userId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(string type, string period, CancellationToken ct = default);
    Task<(IReadOnlyList<NotificationDto> Items, int Unread)> GetNotificationsAsync(Guid userId, CancellationToken ct = default);
    Task MarkNotificationsReadAsync(Guid userId, Guid? notificationId, CancellationToken ct = default);
    Task<CommunityReportDto> CreateReportAsync(Guid userId, string targetType, Guid targetId, string reason, string? description, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityReportDto>> ListReportsAsync(string? status, CancellationToken ct = default);
    Task ModerateAsync(Guid moderatorId, string action, string targetType, Guid targetId, string? resolution, CancellationToken ct = default);
    Task EvaluateAchievementsAsync(Guid userId, CancellationToken ct = default);
}

public sealed class CommunityService(
    IDbConnectionFactory connectionFactory,
    ICacheService cache,
    IOptions<CommunityOptions> optionsAccessor,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CommunityService> logger) : ICommunityService, ICommunityGameSignalsService
{
    private readonly CommunityOptions _options = optionsAccessor.Value;

    public async Task<CommunityHomeDto> GetHomeAsync(Guid? viewerId, CancellationToken ct = default)
    {
        var (feed, _) = await GetFeedAsync(viewerId, null, 12, "newest", ct);
        var trending = await QueryPostsAsync("""
            SELECT p.*, u.username, u.display_name, u.avatar_url, g.slug AS game_slug, g.title AS game_title, g.thumbnail_url
            FROM community_posts p
            INNER JOIN users u ON u.id = p.author_id
            LEFT JOIN games g ON g.id = p.game_id
            WHERE p.deleted_at IS NULL AND p.status = 'published'
            ORDER BY (p.comment_count * 2 + p.reaction_count) DESC, p.created_at DESC
            LIMIT 8
            """, null, viewerId, ct);
        var popular = await GetTrendingGamesAsync(8, ct);
        var achievements = feed.Where(f => f.ActivityType == "achievement_unlocked").Take(6).ToList();
        var challenges = await GetChallengesAsync(viewerId, ct);
        var top = await GetLeaderboardAsync("players", "weekly", ct);
        return new CommunityHomeDto(feed, trending, popular, achievements, challenges.Where(c => c.Status == "active").Take(4).ToList(), top.Take(10).ToList());
    }

    public async Task<IReadOnlyList<CommunityPostDto>> GetGameDiscussionsAsync(
        string gameSlug, string sort, Guid? viewerId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var gameId = await ResolveGameIdAsync(connection, gameSlug, ct);
        var order = sort.ToLowerInvariant() switch
        {
            "popular" => "(p.comment_count * 2 + p.reaction_count) DESC, p.created_at DESC",
            "most_commented" or "most-commented" => "p.comment_count DESC, p.created_at DESC",
            _ => "p.created_at DESC"
        };

        return await QueryPostsAsync($"""
            SELECT p.*, u.username, u.display_name, u.avatar_url, g.slug AS game_slug, g.title AS game_title, g.thumbnail_url
            FROM community_posts p
            INNER JOIN users u ON u.id = p.author_id
            LEFT JOIN games g ON g.id = p.game_id
            WHERE p.deleted_at IS NULL AND p.status = 'published' AND p.game_id = @GameId
            ORDER BY {order}
            LIMIT 50
            """, new { GameId = gameId }, viewerId, ct);
    }

    public async Task<(IReadOnlyList<FeedItemDto> Items, string? NextCursor)> GetFeedAsync(
        Guid? viewerId, string? cursor, int limit, string sort, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var connection = await OpenAsync(ct);
        var take = Math.Clamp(limit, 1, 50);
        DateTimeOffset? before = null;
        if (!string.IsNullOrWhiteSpace(cursor) && DateTimeOffset.TryParse(cursor, out var parsed))
        {
            before = parsed;
        }

        var blocked = viewerId is null ? [] : (await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT blocked_id FROM user_blocks WHERE blocker_id = @UserId UNION SELECT blocker_id FROM user_blocks WHERE blocked_id = @UserId",
            new { UserId = viewerId }, cancellationToken: ct))).ToArray();

        var sql = """
            SELECT a.id, a.activity_type, a.created_at, a.entity_id, a.entity_type, a.metadata,
                   u.id AS user_id, u.username, u.display_name, u.avatar_url,
                   g.id AS game_id, g.slug AS game_slug, g.title AS game_title, g.thumbnail_url,
                   COALESCE(p.reaction_count, 0) AS reaction_count,
                   COALESCE(p.comment_count, 0) AS comment_count
            FROM community_activity a
            INNER JOIN users u ON u.id = a.user_id
            LEFT JOIN games g ON g.id = a.game_id
            LEFT JOIN community_posts p ON a.entity_type = 'post' AND p.id = a.entity_id
            WHERE (@Before IS NULL OR a.created_at < @Before)
              AND (@BlockedCount = 0 OR a.user_id <> ALL(@Blocked))
              AND COALESCE(u.show_activity, TRUE) = TRUE
            ORDER BY a.created_at DESC
            LIMIT @Limit
            """;

        var rows = await connection.QueryAsync(new CommandDefinition(sql, new
        {
            Before = before,
            Blocked = blocked,
            BlockedCount = blocked.Length,
            Limit = take + 1
        }, cancellationToken: ct));

        var items = rows.Select(r =>
        {
            var type = (string)r.activity_type;
            var username = (string)r.username;
            var gameTitle = (string?)r.game_title;
            return new FeedItemDto(
                (Guid)r.id,
                type,
                (DateTimeOffset)r.created_at,
                new CommunityUserDto((Guid)r.user_id, username, (string?)r.display_name, (string?)r.avatar_url),
                r.game_id is null ? null : new CommunityGameDto((Guid)r.game_id, (string)r.game_slug, gameTitle ?? "", (string?)r.thumbnail_url),
                BuildActivityMessage(type, username, gameTitle),
                (int)r.reaction_count,
                (int)r.comment_count,
                (Guid?)r.entity_id,
                (string?)r.entity_type);
        }).ToList();

        string? next = null;
        if (items.Count > take)
        {
            next = items[take - 1].CreatedAt.ToString("O");
            items = items.Take(take).ToList();
        }

        CommunityMetrics.Feed(sw.Elapsed.TotalMilliseconds);
        return (items, next);
    }

    public async Task<CommunityPostDto> GetPostAsync(Guid id, Guid? viewerId, CancellationToken ct = default)
    {
        var posts = await QueryPostsAsync("""
            SELECT p.*, u.username, u.display_name, u.avatar_url, g.slug AS game_slug, g.title AS game_title, g.thumbnail_url
            FROM community_posts p
            INNER JOIN users u ON u.id = p.author_id
            LEFT JOIN games g ON g.id = p.game_id
            WHERE p.id = @Id AND p.deleted_at IS NULL AND p.status <> 'deleted'
            """, new { Id = id }, viewerId, ct);
        var post = posts.FirstOrDefault() ?? throw new NotFoundException("Post", id.ToString());
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE community_posts SET view_count = view_count + 1 WHERE id = @Id",
            new { Id = id }, cancellationToken: ct));
        return post;
    }

    public async Task<CommunityPostDto> CreatePostAsync(Guid userId, string type, string title, string content, Guid? gameId, CancellationToken ct = default)
    {
        if (!PostTypes.All.Contains(type)) throw new ValidationException("Invalid post type.");
        var cleanTitle = ContentSanitizer.SanitizePlainText(title, _options.TitleMaxLength);
        var cleanContent = ContentSanitizer.SanitizePlainText(content, _options.ContentMaxLength);
        if (string.IsNullOrWhiteSpace(cleanTitle) || string.IsNullOrWhiteSpace(cleanContent))
            throw new ValidationException("Title and content are required.");
        if (type is PostTypes.GameShare or PostTypes.Recommendation or PostTypes.Discussion or PostTypes.Question && gameId is null)
        {
            // game optional for discussion/question, required for share/recommendation
            if (type is PostTypes.GameShare or PostTypes.Recommendation)
                throw new ValidationException("gameId is required for this post type.");
        }

        await EnsureRateLimitAsync(userId, "post", _options.PostsPerWindow, _options.PostWindowMinutes, ct);
        await using var connection = await OpenAsync(ct);
        if (gameId is not null)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM games WHERE id = @GameId AND status = 'published')",
                new { GameId = gameId }, cancellationToken: ct));
            if (!exists) throw new NotFoundException("Game", gameId.Value.ToString());
        }

        var id = Guid.NewGuid();
        var slug = $"{ContentSanitizer.ToSlug(cleanTitle)}-{id.ToString("N")[..8]}";
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO community_posts (id, author_id, game_id, type, title, content, slug, status, created_at, updated_at)
            VALUES (@Id, @UserId, @GameId, @Type, @Title, @Content, @Slug, 'published', (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            """, new { Id = id, UserId = userId, GameId = gameId, Type = type.ToLowerInvariant(), Title = cleanTitle, Content = cleanContent, Slug = slug }, cancellationToken: ct));

        await WriteActivityAsync(connection, userId, type == PostTypes.GameShare ? "game_shared" : "discussion_created", gameId, "post", id, ct);
        await NotifyFollowersAsync(connection, userId, "post_created", "post", id, ct);
        CommunityMetrics.PostCreated();
        await EvaluateAchievementsAsync(userId, ct);
        await BumpChallengeAsync(userId, "posts", ct);
        return await GetPostAsync(id, userId, ct);
    }

    public async Task<CommunityPostDto> UpdatePostAsync(Guid userId, Guid postId, string title, string content, bool isMod, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var authorId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT author_id FROM community_posts WHERE id = @Id AND deleted_at IS NULL", new { Id = postId }, cancellationToken: ct));
        if (authorId is null) throw new NotFoundException("Post", postId.ToString());
        if (authorId != userId && !isMod) throw new ForbiddenException("You can only edit your own posts.");
        var cleanTitle = ContentSanitizer.SanitizePlainText(title, _options.TitleMaxLength);
        var cleanContent = ContentSanitizer.SanitizePlainText(content, _options.ContentMaxLength);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE community_posts SET title = @Title, content = @Content, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id
            """, new { Id = postId, Title = cleanTitle, Content = cleanContent }, cancellationToken: ct));
        return await GetPostAsync(postId, userId, ct);
    }

    public async Task DeletePostAsync(Guid userId, Guid postId, bool isMod, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var authorId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT author_id FROM community_posts WHERE id = @Id AND deleted_at IS NULL", new { Id = postId }, cancellationToken: ct));
        if (authorId is null) throw new NotFoundException("Post", postId.ToString());
        if (authorId != userId && !isMod) throw new ForbiddenException("You can only delete your own posts.");
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE community_posts SET status = 'deleted', deleted_at = (NOW() AT TIME ZONE 'utc'), updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id
            """, new { Id = postId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CommunityCommentDto>> GetCommentsAsync(Guid postId, Guid? viewerId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var rows = (await connection.QueryAsync(new CommandDefinition("""
            SELECT c.id, c.post_id, c.parent_id, c.content, c.reaction_count, c.created_at,
                   u.id AS user_id, u.username, u.display_name, u.avatar_url,
                   (
                     SELECT r.reaction FROM community_reactions r
                     WHERE r.user_id = @ViewerId AND r.target_type = 'comment' AND r.target_id = c.id
                     LIMIT 1
                   ) AS viewer_reaction
            FROM community_comments c
            INNER JOIN users u ON u.id = c.author_id
            WHERE c.post_id = @PostId AND c.deleted_at IS NULL AND c.status = 'published'
            ORDER BY c.created_at ASC
            """, new { PostId = postId, ViewerId = viewerId }, cancellationToken: ct))).ToList();

        var roots = new List<CommunityCommentDto>();
        var map = new Dictionary<Guid, CommunityCommentDto>();
        foreach (var r in rows.Where(x => x.parent_id is null))
        {
            var dto = MapComment(r, Array.Empty<CommunityCommentDto>());
            map[(Guid)r.id] = dto;
            roots.Add(dto);
        }

        foreach (var r in rows.Where(x => x.parent_id is not null))
        {
            var parentId = (Guid)r.parent_id!;
            if (!map.TryGetValue(parentId, out var parent)) continue;
            var replies = parent.Replies.ToList();
            replies.Add(MapComment(r, Array.Empty<CommunityCommentDto>()));
            map[parentId] = parent with { Replies = replies };
            var idx = roots.FindIndex(x => x.Id == parentId);
            if (idx >= 0) roots[idx] = map[parentId];
        }

        return roots;
    }

    public async Task<CommunityCommentDto> CreateCommentAsync(Guid userId, Guid postId, string content, Guid? parentId, CancellationToken ct = default)
    {
        var clean = ContentSanitizer.SanitizePlainText(content, _options.CommentMaxLength);
        if (string.IsNullOrWhiteSpace(clean)) throw new ValidationException("Comment content is required.");
        await EnsureRateLimitAsync(userId, "comment", _options.CommentsPerWindow, _options.CommentWindowMinutes, ct);
        await using var connection = await OpenAsync(ct);
        var post = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT id, author_id, game_id, status FROM community_posts WHERE id = @Id AND deleted_at IS NULL",
            new { Id = postId }, cancellationToken: ct))
            ?? throw new NotFoundException("Post", postId.ToString());
        if ((string)post.status != "published") throw new ValidationException("Cannot comment on this post.");

        if (parentId is not null)
        {
            var parent = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
                "SELECT id, parent_id FROM community_comments WHERE id = @Id AND post_id = @PostId AND deleted_at IS NULL",
                new { Id = parentId, PostId = postId }, cancellationToken: ct))
                ?? throw new NotFoundException("Comment", parentId.Value.ToString());
            if (parent.parent_id is not null)
                throw new ValidationException("Maximum comment nesting is 2 levels.");
        }

        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO community_comments (id, post_id, author_id, parent_id, content, created_at, updated_at)
            VALUES (@Id, @PostId, @UserId, @ParentId, @Content, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'));
            UPDATE community_posts SET comment_count = comment_count + 1, updated_at = (NOW() AT TIME ZONE 'utc') WHERE id = @PostId;
            """, new { Id = id, PostId = postId, UserId = userId, ParentId = parentId, Content = clean }, cancellationToken: ct));

        await WriteActivityAsync(connection, userId, "comment_created", (Guid?)post.game_id, "comment", id, ct);
        if ((Guid)post.author_id != userId)
        {
            await CreateNotificationAsync(connection, (Guid)post.author_id, parentId is null ? "comment_on_post" : "reply_to_comment", userId, parentId is null ? "post" : "comment", parentId ?? postId, ct);
        }

        CommunityMetrics.CommentCreated();
        await EvaluateAchievementsAsync(userId, ct);
        var comments = await GetCommentsAsync(postId, userId, ct);
        return comments.SelectMany(c => c.Replies.Prepend(c)).First(c => c.Id == id);
    }

    public async Task DeleteCommentAsync(Guid userId, Guid commentId, bool isMod, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT author_id, post_id FROM community_comments WHERE id = @Id AND deleted_at IS NULL",
            new { Id = commentId }, cancellationToken: ct))
            ?? throw new NotFoundException("Comment", commentId.ToString());
        if ((Guid)row.author_id != userId && !isMod) throw new ForbiddenException("You can only delete your own comments.");
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE community_comments SET status = 'deleted', deleted_at = (NOW() AT TIME ZONE 'utc') WHERE id = @Id;
            UPDATE community_posts SET comment_count = GREATEST(comment_count - 1, 0) WHERE id = @PostId;
            """, new { Id = commentId, PostId = (Guid)row.post_id }, cancellationToken: ct));
    }

    public async Task SetReactionAsync(Guid userId, string targetType, Guid targetId, string reaction, CancellationToken ct = default)
    {
        targetType = targetType.ToLowerInvariant();
        reaction = reaction.ToLowerInvariant();
        if (targetType is not ("post" or "comment" or "review")) throw new ValidationException("Invalid target type.");
        if (!ReactionKinds.All.Contains(reaction)) throw new ValidationException("Invalid reaction.");
        await using var connection = await OpenAsync(ct);
        await EnsureTargetExistsAsync(connection, targetType, targetId, ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO community_reactions (id, user_id, target_type, target_id, reaction, created_at)
            VALUES (@Id, @UserId, @TargetType, @TargetId, @Reaction, (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (user_id, target_type, target_id)
            DO UPDATE SET reaction = EXCLUDED.reaction;
            """, new { Id = Guid.NewGuid(), UserId = userId, TargetType = targetType, TargetId = targetId, Reaction = reaction }, cancellationToken: ct));
        await RefreshReactionCountAsync(connection, targetType, targetId, ct);
    }

    public async Task RemoveReactionAsync(Guid userId, string targetType, Guid targetId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM community_reactions WHERE user_id = @UserId AND target_type = @TargetType AND target_id = @TargetId",
            new { UserId = userId, TargetType = targetType.ToLowerInvariant(), TargetId = targetId }, cancellationToken: ct));
        await RefreshReactionCountAsync(connection, targetType.ToLowerInvariant(), targetId, ct);
    }

    public async Task<(GameReviewSummaryDto Summary, IReadOnlyList<GameReviewDto> Items)> GetReviewsAsync(string gameSlug, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var gameId = await ResolveGameIdAsync(connection, gameSlug, ct);
        var cacheKey = $"community:review-summary:{gameId}";
        var summary = await cache.GetAsync<GameReviewSummaryDto>(cacheKey, ct);
        if (summary is null)
        {
            summary = await connection.QuerySingleAsync<GameReviewSummaryDto>(new CommandDefinition("""
                SELECT @GameId AS GameId,
                       COALESCE(AVG(rating)::float, 0) AS AverageRating,
                       COUNT(*)::int AS ReviewCount
                FROM game_reviews
                WHERE game_id = @GameId AND deleted_at IS NULL AND status = 'published'
                """, new { GameId = gameId }, cancellationToken: ct));
            await cache.SetAsync(cacheKey, summary, TimeSpan.FromSeconds(_options.ReviewSummaryCacheSeconds), ct);
        }

        var items = (await connection.QueryAsync(new CommandDefinition("""
            SELECT r.id, r.game_id, r.rating, r.content, r.created_at, r.updated_at,
                   u.id AS user_id, u.username, u.display_name, u.avatar_url
            FROM game_reviews r
            INNER JOIN users u ON u.id = r.user_id
            WHERE r.game_id = @GameId AND r.deleted_at IS NULL AND r.status = 'published'
            ORDER BY r.created_at DESC
            LIMIT 50
            """, new { GameId = gameId }, cancellationToken: ct)))
            .Select(r => new GameReviewDto(
                (Guid)r.id, (Guid)r.game_id, (int)r.rating, (string)r.content,
                (DateTimeOffset)r.created_at, (DateTimeOffset)r.updated_at,
                new CommunityUserDto((Guid)r.user_id, (string)r.username, (string?)r.display_name, (string?)r.avatar_url)))
            .ToList();

        return (summary, items);
    }

    public async Task<GameReviewDto> UpsertReviewAsync(Guid userId, string gameSlug, int rating, string content, CancellationToken ct = default)
    {
        if (rating is < 1 or > 5) throw new ValidationException("Rating must be between 1 and 5.");
        var clean = ContentSanitizer.SanitizePlainText(content, _options.ContentMaxLength);
        if (string.IsNullOrWhiteSpace(clean)) throw new ValidationException("Review content is required.");
        await EnsureRateLimitAsync(userId, "review", _options.ReviewsPerWindow, _options.ReviewWindowMinutes, ct);
        await using var connection = await OpenAsync(ct);
        var gameId = await ResolveGameIdAsync(connection, gameSlug, ct);
        if (_options.RequirePlayedForReview)
        {
            var played = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM user_play_history WHERE user_id = @UserId AND game_id = @GameId)",
                new { UserId = userId, GameId = gameId }, cancellationToken: ct));
            if (!played) throw new ValidationException("Play the game before reviewing.");
        }

        var id = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM game_reviews WHERE game_id = @GameId AND user_id = @UserId",
            new { GameId = gameId, UserId = userId }, cancellationToken: ct));
        var isNew = id is null;
        if (isNew)
        {
            id = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO game_reviews (id, game_id, user_id, rating, content, created_at, updated_at)
                VALUES (@Id, @GameId, @UserId, @Rating, @Content, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
                """, new { Id = id, GameId = gameId, UserId = userId, Rating = rating, Content = clean }, cancellationToken: ct));
            CommunityMetrics.ReviewCreated();
            await WriteActivityAsync(connection, userId, "game_reviewed", gameId, "review", id.Value, ct);
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE game_reviews
                SET rating = @Rating, content = @Content, status = 'published', deleted_at = NULL, updated_at = (NOW() AT TIME ZONE 'utc')
                WHERE id = @Id
                """, new { Id = id, Rating = rating, Content = clean }, cancellationToken: ct));
        }

        await cache.RemoveAsync($"community:review-summary:{gameId}", ct);
        if (isNew)
        {
            // XP is server-authoritative; edits do not re-award.
            await AwardReviewXpAsync(userId, gameId, ct);
        }

        await EvaluateAchievementsAsync(userId, ct);
        var (_, items) = await GetReviewsAsync(gameSlug, ct);
        return items.First(i => i.Id == id);
    }

    private async Task AwardReviewXpAsync(Guid userId, Guid gameId, CancellationToken ct)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var xpEngine = scope.ServiceProvider.GetRequiredService<IXpEngine>();
        await xpEngine.ProcessRatingCreatedAsync(userId, gameId, ct);
        await xpEngine.ProcessReviewCreatedAsync(userId, gameId, ct);
    }

    public async Task DeleteReviewAsync(Guid userId, Guid reviewId, bool isMod, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT user_id, game_id FROM game_reviews WHERE id = @Id AND deleted_at IS NULL",
            new { Id = reviewId }, cancellationToken: ct))
            ?? throw new NotFoundException("Review", reviewId.ToString());
        if ((Guid)row.user_id != userId && !isMod) throw new ForbiddenException("You can only delete your own reviews.");
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE game_reviews SET status = 'deleted', deleted_at = (NOW() AT TIME ZONE 'utc') WHERE id = @Id
            """, new { Id = reviewId }, cancellationToken: ct));
        await cache.RemoveAsync($"community:review-summary:{(Guid)row.game_id}", ct);
    }

    public async Task<UserProfileDto> GetProfileAsync(string username, Guid? viewerId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var user = await connection.QuerySingleOrDefaultAsync(new CommandDefinition("""
            SELECT id, username, display_name, avatar_url, bio, created_at, profile_visibility,
                   show_favorites, show_history, show_achievements, show_activity, show_on_leaderboards
            FROM users WHERE LOWER(username) = LOWER(@Username) AND status = 'active'
            """, new { Username = username }, cancellationToken: ct))
            ?? throw new NotFoundException("User", username);

        if ((string)user.profile_visibility == "private" && viewerId != (Guid)user.id)
            throw new ForbiddenException("This profile is private.");

        var userId = (Guid)user.id;
        var isFollowing = viewerId is not null && await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM user_follows WHERE follower_id = @Viewer AND following_id = @UserId)",
            new { Viewer = viewerId, UserId = userId }, cancellationToken: ct));
        var isBlocked = viewerId is not null && await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM user_blocks WHERE (blocker_id = @Viewer AND blocked_id = @UserId) OR (blocker_id = @UserId AND blocked_id = @Viewer))",
            new { Viewer = viewerId, UserId = userId }, cancellationToken: ct));

        var stats = await connection.QuerySingleAsync(new CommandDefinition("""
            SELECT
              (SELECT COUNT(*) FROM user_play_history WHERE user_id = @UserId) AS games_played,
              (SELECT COUNT(*) FROM user_favorites WHERE user_id = @UserId) AS favorites,
              (SELECT COUNT(*) FROM user_achievements WHERE user_id = @UserId) AS achievements,
              (SELECT COUNT(*) FROM game_reviews WHERE user_id = @UserId AND deleted_at IS NULL) AS reviews,
              (SELECT COUNT(*) FROM user_follows WHERE following_id = @UserId) AS followers,
              (SELECT COUNT(*) FROM user_follows WHERE follower_id = @UserId) AS following
            """, new { UserId = userId }, cancellationToken: ct));

        var favorites = new List<CommunityGameDto>();
        if ((bool)user.show_favorites || viewerId == userId)
        {
            favorites = (await connection.QueryAsync<CommunityGameDto>(new CommandDefinition("""
                SELECT g.id AS Id, g.slug AS Slug, g.title AS Title, g.thumbnail_url AS ThumbnailUrl
                FROM user_favorites f
                INNER JOIN games g ON g.id = f.game_id
                WHERE f.user_id = @UserId AND g.status = 'published'
                ORDER BY f.created_at DESC LIMIT 12
                """, new { UserId = userId }, cancellationToken: ct))).ToList();
        }

        var achievements = new List<AchievementDto>();
        if ((bool)user.show_achievements || viewerId == userId)
        {
            achievements = (await GetAchievementsAsync(userId, ct)).Where(a => a.UnlockedAt is not null).Take(12).ToList();
        }

        return new UserProfileDto(
            userId, (string)user.username, (string?)user.display_name, (string?)user.avatar_url, (string?)user.bio,
            (DateTimeOffset)user.created_at,
            (int)stats.games_played, (int)stats.favorites, (int)stats.achievements, (int)stats.reviews,
            (int)stats.followers, (int)stats.following, isFollowing, isBlocked,
            (bool)user.show_favorites, (bool)user.show_history, (bool)user.show_achievements, (bool)user.show_activity,
            favorites, achievements);
    }

    public async Task UpdatePrivacyAsync(Guid userId, bool? showFavorites, bool? showHistory, bool? showAchievements, bool? showActivity, bool? showOnLeaderboards, string? bio, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE users SET
              show_favorites = COALESCE(@ShowFavorites, show_favorites),
              show_history = COALESCE(@ShowHistory, show_history),
              show_achievements = COALESCE(@ShowAchievements, show_achievements),
              show_activity = COALESCE(@ShowActivity, show_activity),
              show_on_leaderboards = COALESCE(@ShowOnLeaderboards, show_on_leaderboards),
              bio = COALESCE(@Bio, bio),
              updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @UserId
            """, new
        {
            UserId = userId,
            ShowFavorites = showFavorites,
            ShowHistory = showHistory,
            ShowAchievements = showAchievements,
            ShowActivity = showActivity,
            ShowOnLeaderboards = showOnLeaderboards,
            Bio = bio is null ? null : ContentSanitizer.SanitizePlainText(bio, _options.BioMaxLength)
        }, cancellationToken: ct));
    }

    public async Task FollowAsync(Guid userId, Guid targetId, CancellationToken ct = default)
    {
        if (userId == targetId) throw new ValidationException("You cannot follow yourself.");
        await using var connection = await OpenAsync(ct);
        await EnsureNotBlockedAsync(connection, userId, targetId, ct);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM users WHERE id = @Id AND status = 'active')", new { Id = targetId }, cancellationToken: ct));
        if (!exists) throw new NotFoundException("User", targetId.ToString());
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO user_follows (follower_id, following_id, created_at)
            VALUES (@UserId, @TargetId, (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT DO NOTHING
            """, new { UserId = userId, TargetId = targetId }, cancellationToken: ct));
        await CreateNotificationAsync(connection, targetId, "user_followed", userId, "user", userId, ct);
    }

    public async Task UnfollowAsync(Guid userId, Guid targetId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM user_follows WHERE follower_id = @UserId AND following_id = @TargetId",
            new { UserId = userId, TargetId = targetId }, cancellationToken: ct));
    }

    public async Task BlockAsync(Guid userId, Guid targetId, CancellationToken ct = default)
    {
        if (userId == targetId) throw new ValidationException("You cannot block yourself.");
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO user_blocks (blocker_id, blocked_id, created_at)
            VALUES (@UserId, @TargetId, (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT DO NOTHING;
            DELETE FROM user_follows
            WHERE (follower_id = @UserId AND following_id = @TargetId)
               OR (follower_id = @TargetId AND following_id = @UserId);
            """, new { UserId = userId, TargetId = targetId }, cancellationToken: ct));
    }

    public async Task UnblockAsync(Guid userId, Guid targetId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM user_blocks WHERE blocker_id = @UserId AND blocked_id = @TargetId",
            new { UserId = userId, TargetId = targetId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AchievementDto>> GetAchievementsAsync(Guid? userId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync(new CommandDefinition("""
            SELECT a.id, a.code, a.name, a.description, a.icon, a.rarity, ua.unlocked_at
            FROM achievements a
            LEFT JOIN user_achievements ua ON ua.achievement_id = a.id AND ua.user_id = @UserId
            WHERE a.is_active = TRUE
            ORDER BY a.name
            """, new { UserId = userId }, cancellationToken: ct));
        return rows.Select(r => new AchievementDto(
            (Guid)r.id, (string)r.code, (string)r.name, (string)r.description,
            (string)r.icon, (string)r.rarity, (DateTimeOffset?)r.unlocked_at)).ToList();
    }

    public async Task<IReadOnlyList<ChallengeDto>> GetChallengesAsync(Guid? userId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync(new CommandDefinition("""
            SELECT c.id, c.title, c.description, c.type, c.target_value, c.start_at, c.end_at, c.status,
                   COALESCE(uc.progress, 0) AS progress, uc.completed_at
            FROM challenges c
            LEFT JOIN user_challenges uc ON uc.challenge_id = c.id AND uc.user_id = @UserId
            WHERE c.status IN ('active','ended')
            ORDER BY c.start_at DESC
            """, new { UserId = userId }, cancellationToken: ct));
        return rows.Select(r => new ChallengeDto(
            (Guid)r.id, (string)r.title, (string)r.description, (string)r.type, (int)r.target_value,
            (DateTimeOffset)r.start_at, (DateTimeOffset)r.end_at, (string)r.status,
            (int)r.progress, r.completed_at is not null)).ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(string type, string period, CancellationToken ct = default)
    {
        var cacheKey = $"community:leaderboard:{type}:{period}";
        var cached = await cache.GetAsync<List<LeaderboardEntryDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        await using var connection = await OpenAsync(ct);
        var since = period.ToLowerInvariant() switch
        {
            "daily" => DateTimeOffset.UtcNow.AddDays(-1),
            "monthly" => DateTimeOffset.UtcNow.AddDays(-30),
            "all" or "all-time" => DateTimeOffset.MinValue,
            _ => DateTimeOffset.UtcNow.AddDays(-7)
        };

        var sql = type.ToLowerInvariant() switch
        {
            "reviewers" => """
                SELECT u.id AS user_id, u.username, u.display_name, u.avatar_url, COUNT(*)::int AS score
                FROM game_reviews r INNER JOIN users u ON u.id = r.user_id
                WHERE r.deleted_at IS NULL AND r.created_at >= @Since AND u.show_on_leaderboards = TRUE
                GROUP BY u.id, u.username, u.display_name, u.avatar_url
                ORDER BY score DESC LIMIT 50
                """,
            "contributors" => """
                SELECT u.id AS user_id, u.username, u.display_name, u.avatar_url, COUNT(*)::int AS score
                FROM community_posts p INNER JOIN users u ON u.id = p.author_id
                WHERE p.deleted_at IS NULL AND p.created_at >= @Since AND u.show_on_leaderboards = TRUE
                GROUP BY u.id, u.username, u.display_name, u.avatar_url
                ORDER BY score DESC LIMIT 50
                """,
            "explorers" => """
                SELECT u.id AS user_id, u.username, u.display_name, u.avatar_url, COUNT(DISTINCT h.game_id)::int AS score
                FROM user_play_history h INNER JOIN users u ON u.id = h.user_id
                WHERE h.played_at >= @Since AND u.show_on_leaderboards = TRUE
                GROUP BY u.id, u.username, u.display_name, u.avatar_url
                ORDER BY score DESC LIMIT 50
                """,
            _ => """
                SELECT u.id AS user_id, u.username, u.display_name, u.avatar_url, COUNT(*)::int AS score
                FROM user_play_history h INNER JOIN users u ON u.id = h.user_id
                WHERE h.played_at >= @Since AND u.show_on_leaderboards = TRUE
                GROUP BY u.id, u.username, u.display_name, u.avatar_url
                ORDER BY score DESC LIMIT 50
                """
        };

        var rows = (await connection.QueryAsync(new CommandDefinition(sql, new { Since = since }, cancellationToken: ct))).ToList();
        var result = rows.Select((r, i) => new LeaderboardEntryDto(
            i + 1, (Guid)r.user_id, (string)r.username, (string?)r.display_name, (string?)r.avatar_url, (int)r.score)).ToList();
        await cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(_options.LeaderboardCacheSeconds), ct);
        return result;
    }

    public async Task<(IReadOnlyList<NotificationDto> Items, int Unread)> GetNotificationsAsync(Guid userId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync(new CommandDefinition("""
            SELECT n.id, n.type, n.actor_id, n.entity_type, n.entity_id, n.created_at, n.read_at, u.username AS actor_username
            FROM notifications n
            LEFT JOIN users u ON u.id = n.actor_id
            WHERE n.user_id = @UserId
            ORDER BY n.created_at DESC
            LIMIT 50
            """, new { UserId = userId }, cancellationToken: ct));
        var items = rows.Select(r => new NotificationDto(
            (Guid)r.id, (string)r.type, (Guid?)r.actor_id, (string?)r.actor_username,
            (string)r.entity_type, (Guid)r.entity_id, (DateTimeOffset)r.created_at, (DateTimeOffset?)r.read_at,
            BuildNotificationMessage((string)r.type, (string?)r.actor_username))).ToList();
        var unread = items.Count(i => i.ReadAt is null);
        return (items, unread);
    }

    public async Task MarkNotificationsReadAsync(Guid userId, Guid? notificationId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        if (notificationId is null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE notifications SET read_at = (NOW() AT TIME ZONE 'utc') WHERE user_id = @UserId AND read_at IS NULL",
                new { UserId = userId }, cancellationToken: ct));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE notifications SET read_at = (NOW() AT TIME ZONE 'utc') WHERE user_id = @UserId AND id = @Id",
                new { UserId = userId, Id = notificationId }, cancellationToken: ct));
        }
    }

    public async Task<CommunityReportDto> CreateReportAsync(Guid userId, string targetType, Guid targetId, string reason, string? description, CancellationToken ct = default)
    {
        var allowedReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "spam","harassment","hate","sexual","violence","scam","cheating","misleading","other" };
        if (!allowedReasons.Contains(reason)) throw new ValidationException("Invalid report reason.");
        await using var connection = await OpenAsync(ct);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO community_reports (id, reporter_id, target_type, target_id, reason, description, status, created_at)
            VALUES (@Id, @UserId, @TargetType, @TargetId, @Reason, @Description, 'pending', (NOW() AT TIME ZONE 'utc'))
            """, new
        {
            Id = id,
            UserId = userId,
            TargetType = targetType.ToLowerInvariant(),
            TargetId = targetId,
            Reason = reason.ToLowerInvariant(),
            Description = ContentSanitizer.SanitizePlainText(description, 1000)
        }, cancellationToken: ct));
        CommunityMetrics.ReportCreated();
        return new CommunityReportDto(id, targetType.ToLowerInvariant(), targetId, reason.ToLowerInvariant(), "pending", DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<CommunityReportDto>> ListReportsAsync(string? status, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync(new CommandDefinition("""
            SELECT id, target_type, target_id, reason, status, created_at
            FROM community_reports
            WHERE (@Status IS NULL OR status = @Status)
            ORDER BY created_at DESC
            LIMIT 100
            """, new { Status = string.IsNullOrWhiteSpace(status) ? null : status.ToLowerInvariant() }, cancellationToken: ct));
        return rows.Select(r => new CommunityReportDto((Guid)r.id, (string)r.target_type, (Guid)r.target_id, (string)r.reason, (string)r.status, (DateTimeOffset)r.created_at)).ToList();
    }

    public async Task ModerateAsync(Guid moderatorId, string action, string targetType, Guid targetId, string? resolution, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        switch ((action.ToLowerInvariant(), targetType.ToLowerInvariant()))
        {
            case ("hide", "post"):
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE community_posts SET status = 'hidden', updated_at = (NOW() AT TIME ZONE 'utc') WHERE id = @Id",
                    new { Id = targetId }, cancellationToken: ct));
                break;
            case ("delete", "post"):
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE community_posts SET status = 'deleted', deleted_at = (NOW() AT TIME ZONE 'utc') WHERE id = @Id",
                    new { Id = targetId }, cancellationToken: ct));
                break;
            case ("delete", "comment"):
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE community_comments SET status = 'deleted', deleted_at = (NOW() AT TIME ZONE 'utc') WHERE id = @Id",
                    new { Id = targetId }, cancellationToken: ct));
                break;
            case ("delete", "review"):
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE game_reviews SET status = 'deleted', deleted_at = (NOW() AT TIME ZONE 'utc') WHERE id = @Id",
                    new { Id = targetId }, cancellationToken: ct));
                break;
            case ("resolve", "report"):
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE community_reports
                    SET status = 'resolved', moderator_id = @ModeratorId, resolution = @Resolution, resolved_at = (NOW() AT TIME ZONE 'utc')
                    WHERE id = @Id
                    """, new { Id = targetId, ModeratorId = moderatorId, Resolution = resolution }, cancellationToken: ct));
                break;
            case ("reject", "report"):
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE community_reports
                    SET status = 'rejected', moderator_id = @ModeratorId, resolution = @Resolution, resolved_at = (NOW() AT TIME ZONE 'utc')
                    WHERE id = @Id
                    """, new { Id = targetId, ModeratorId = moderatorId, Resolution = resolution }, cancellationToken: ct));
                break;
            default:
                throw new ValidationException("Unsupported moderation action.");
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO moderation_audit_logs (id, actor_id, action, target_type, target_id, details, created_at)
            VALUES (@Id, @ActorId, @Action, @TargetType, @TargetId, CAST(@Details AS jsonb), (NOW() AT TIME ZONE 'utc'))
            """, new
        {
            Id = Guid.NewGuid(),
            ActorId = moderatorId,
            Action = action.ToLowerInvariant(),
            TargetType = targetType.ToLowerInvariant(),
            TargetId = targetId,
            Details = JsonSerializer.Serialize(new { resolution })
        }, cancellationToken: ct));
    }

    public async Task EvaluateAchievementsAsync(Guid userId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        var stats = await connection.QuerySingleAsync(new CommandDefinition("""
            SELECT
              (SELECT COUNT(DISTINCT game_id) FROM user_play_history WHERE user_id = @UserId) AS games_played,
              (SELECT COUNT(*) FROM user_favorites WHERE user_id = @UserId) AS favorites,
              (SELECT COUNT(*) FROM game_reviews WHERE user_id = @UserId AND deleted_at IS NULL) AS reviews,
              (SELECT COUNT(*) FROM community_posts WHERE author_id = @UserId AND deleted_at IS NULL) AS posts,
              (SELECT COUNT(*) FROM community_comments WHERE author_id = @UserId AND deleted_at IS NULL) AS comments
            """, new { UserId = userId }, cancellationToken: ct));

        var defs = await connection.QueryAsync(new CommandDefinition(
            "SELECT id, code, requirement_type, requirement_value FROM achievements WHERE is_active = TRUE",
            cancellationToken: ct));

        foreach (var def in defs)
        {
            var current = ((string)def.requirement_type) switch
            {
                "games_played" => (int)stats.games_played,
                "favorites" => (int)stats.favorites,
                "reviews" => (int)stats.reviews,
                "posts" => (int)stats.posts,
                "comments" => (int)stats.comments,
                _ => 0
            };
            if (current < (int)def.requirement_value) continue;
            var inserted = await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO user_achievements (user_id, achievement_id, unlocked_at)
                VALUES (@UserId, @AchievementId, (NOW() AT TIME ZONE 'utc'))
                ON CONFLICT DO NOTHING
                """, new { UserId = userId, AchievementId = (Guid)def.id }, cancellationToken: ct));
            if (inserted > 0)
            {
                await WriteActivityAsync(connection, userId, "achievement_unlocked", null, "achievement", (Guid)def.id, ct);
                await CreateNotificationAsync(connection, userId, "achievement_unlocked", null, "achievement", (Guid)def.id, ct);
            }
        }
    }

    public async Task<GameReviewSummaryDto?> GetGameEngagementAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var (summary, _) = await GetReviewsByGameIdAsync(gameId, cancellationToken);
        return summary;
    }

    public async Task<IReadOnlyList<CommunityGameDto>> GetTrendingGamesAsync(int limit, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"community:trending-games:{limit}";
        var cached = await cache.GetAsync<List<CommunityGameDto>>(cacheKey, cancellationToken);
        if (cached is not null) return cached;
        await using var connection = await OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<CommunityGameDto>(new CommandDefinition("""
            SELECT g.id AS Id, g.slug AS Slug, g.title AS Title, g.thumbnail_url AS ThumbnailUrl
            FROM games g
            LEFT JOIN community_posts p ON p.game_id = g.id AND p.deleted_at IS NULL AND p.created_at > (NOW() AT TIME ZONE 'utc') - INTERVAL '14 days'
            LEFT JOIN game_reviews r ON r.game_id = g.id AND r.deleted_at IS NULL AND r.created_at > (NOW() AT TIME ZONE 'utc') - INTERVAL '30 days'
            WHERE g.status = 'published'
            GROUP BY g.id, g.slug, g.title, g.thumbnail_url
            ORDER BY (COUNT(DISTINCT p.id) * 2 + COUNT(DISTINCT r.id)) DESC, g.updated_at DESC
            LIMIT @Limit
            """, new { Limit = Math.Clamp(limit, 1, 50) }, cancellationToken: cancellationToken))).ToList();
        await cache.SetAsync(cacheKey, rows, TimeSpan.FromSeconds(_options.TrendingCacheSeconds), cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetCommunitySignalsAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var row = await connection.QuerySingleAsync(new CommandDefinition("""
            SELECT
              (SELECT COUNT(*) FROM community_posts WHERE game_id = @GameId AND deleted_at IS NULL) AS discussion_count,
              (SELECT COUNT(*) FROM game_reviews WHERE game_id = @GameId AND deleted_at IS NULL) AS review_count,
              (SELECT COALESCE(AVG(rating)::float, 0) FROM game_reviews WHERE game_id = @GameId AND deleted_at IS NULL) AS average_rating,
              (SELECT COUNT(*) FROM community_posts WHERE game_id = @GameId AND type = 'game_share' AND deleted_at IS NULL) AS share_count
            """, new { GameId = gameId }, cancellationToken: cancellationToken));
        return new Dictionary<string, object?>
        {
            ["discussionCount"] = (long)row.discussion_count,
            ["reviewCount"] = (long)row.review_count,
            ["averageRating"] = (double)row.average_rating,
            ["shareCount"] = (long)row.share_count
        };
    }

    private async Task<(GameReviewSummaryDto Summary, IReadOnlyList<GameReviewDto> Items)> GetReviewsByGameIdAsync(Guid gameId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var summary = await connection.QuerySingleAsync<GameReviewSummaryDto>(new CommandDefinition("""
            SELECT @GameId AS GameId, COALESCE(AVG(rating)::float, 0) AS AverageRating, COUNT(*)::int AS ReviewCount
            FROM game_reviews WHERE game_id = @GameId AND deleted_at IS NULL AND status = 'published'
            """, new { GameId = gameId }, cancellationToken: ct));
        return (summary, []);
    }

    private async Task<List<CommunityPostDto>> QueryPostsAsync(string sql, object? param, Guid? viewerId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync(new CommandDefinition(sql, param, cancellationToken: ct));
        var list = new List<CommunityPostDto>();
        foreach (var r in rows)
        {
            string? viewerReaction = null;
            if (viewerId is not null)
            {
                viewerReaction = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                    "SELECT reaction FROM community_reactions WHERE user_id = @UserId AND target_type = 'post' AND target_id = @Id LIMIT 1",
                    new { UserId = viewerId, Id = (Guid)r.id }, cancellationToken: ct));
            }

            list.Add(new CommunityPostDto(
                (Guid)r.id, (string)r.slug, (string)r.type, (string)r.title, (string)r.content, (string)r.status,
                (int)r.comment_count, (int)r.reaction_count, (int)r.view_count,
                (DateTimeOffset)r.created_at, (DateTimeOffset)r.updated_at,
                new CommunityUserDto((Guid)r.author_id, (string)r.username, (string?)r.display_name, (string?)r.avatar_url),
                r.game_id is null ? null : new CommunityGameDto((Guid)r.game_id, (string)r.game_slug, (string)r.game_title, (string?)r.thumbnail_url),
                viewerReaction));
        }

        return list;
    }

    private async Task EnsureRateLimitAsync(Guid userId, string action, int limit, int windowMinutes, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var sql = action switch
        {
            "post" => "SELECT COUNT(*) FROM community_posts WHERE author_id = @UserId AND created_at > (NOW() AT TIME ZONE 'utc') - (@Minutes || ' minutes')::interval",
            "comment" => "SELECT COUNT(*) FROM community_comments WHERE author_id = @UserId AND created_at > (NOW() AT TIME ZONE 'utc') - (@Minutes || ' minutes')::interval",
            "review" => "SELECT COUNT(*) FROM game_reviews WHERE user_id = @UserId AND updated_at > (NOW() AT TIME ZONE 'utc') - (@Minutes || ' minutes')::interval",
            _ => throw new InvalidOperationException()
        };
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { UserId = userId, Minutes = windowMinutes.ToString() }, cancellationToken: ct));
        if (count >= limit) throw new ValidationException("Rate limit exceeded. Please try again later.");
    }

    private async Task BumpChallengeAsync(Guid userId, string type, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO user_challenges (challenge_id, user_id, progress, updated_at)
            SELECT c.id, @UserId, 1, (NOW() AT TIME ZONE 'utc')
            FROM challenges c
            WHERE c.status = 'active' AND c.type = @Type AND c.end_at > (NOW() AT TIME ZONE 'utc')
            ON CONFLICT (challenge_id, user_id) DO UPDATE
            SET progress = LEAST(user_challenges.progress + 1, EXCLUDED.progress + 1000),
                updated_at = (NOW() AT TIME ZONE 'utc'),
                completed_at = CASE
                    WHEN user_challenges.completed_at IS NOT NULL THEN user_challenges.completed_at
                    WHEN user_challenges.progress + 1 >= (SELECT target_value FROM challenges WHERE id = user_challenges.challenge_id)
                        THEN (NOW() AT TIME ZONE 'utc')
                    ELSE NULL END
            """, new { UserId = userId, Type = type }, cancellationToken: ct));
    }

    private static async Task WriteActivityAsync(DbConnection connection, Guid userId, string type, Guid? gameId, string? entityType, Guid? entityId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO community_activity (id, user_id, activity_type, game_id, entity_type, entity_id, metadata, created_at)
            VALUES (@Id, @UserId, @Type, @GameId, @EntityType, @EntityId, '{}'::jsonb, (NOW() AT TIME ZONE 'utc'))
            """, new { Id = Guid.NewGuid(), UserId = userId, Type = type, GameId = gameId, EntityType = entityType, EntityId = entityId }, cancellationToken: ct));
    }

    private static async Task CreateNotificationAsync(DbConnection connection, Guid userId, string type, Guid? actorId, string entityType, Guid entityId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO notifications (id, user_id, type, actor_id, entity_type, entity_id, metadata, created_at)
            VALUES (@Id, @UserId, @Type, @ActorId, @EntityType, @EntityId, '{}'::jsonb, (NOW() AT TIME ZONE 'utc'))
            """, new { Id = Guid.NewGuid(), UserId = userId, Type = type, ActorId = actorId, EntityType = entityType, EntityId = entityId }, cancellationToken: ct));
    }

    private static async Task NotifyFollowersAsync(DbConnection connection, Guid userId, string type, string entityType, Guid entityId, CancellationToken ct)
    {
        var followers = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT follower_id FROM user_follows WHERE following_id = @UserId LIMIT 100",
            new { UserId = userId }, cancellationToken: ct));
        foreach (var follower in followers)
        {
            await CreateNotificationAsync(connection, follower, type, userId, entityType, entityId, ct);
        }
    }

    private static async Task EnsureTargetExistsAsync(DbConnection connection, string targetType, Guid targetId, CancellationToken ct)
    {
        var sql = targetType switch
        {
            "post" => "SELECT EXISTS(SELECT 1 FROM community_posts WHERE id = @Id AND deleted_at IS NULL)",
            "comment" => "SELECT EXISTS(SELECT 1 FROM community_comments WHERE id = @Id AND deleted_at IS NULL)",
            "review" => "SELECT EXISTS(SELECT 1 FROM game_reviews WHERE id = @Id AND deleted_at IS NULL)",
            _ => throw new ValidationException("Invalid target.")
        };
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { Id = targetId }, cancellationToken: ct));
        if (!exists) throw new NotFoundException(targetType, targetId.ToString());
    }

    private static async Task RefreshReactionCountAsync(DbConnection connection, string targetType, Guid targetId, CancellationToken ct)
    {
        if (targetType == "post")
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE community_posts SET reaction_count = (
                  SELECT COUNT(*) FROM community_reactions WHERE target_type = 'post' AND target_id = @Id
                ) WHERE id = @Id
                """, new { Id = targetId }, cancellationToken: ct));
        }
        else if (targetType == "comment")
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE community_comments SET reaction_count = (
                  SELECT COUNT(*) FROM community_reactions WHERE target_type = 'comment' AND target_id = @Id
                ) WHERE id = @Id
                """, new { Id = targetId }, cancellationToken: ct));
        }
    }

    private static async Task<Guid> ResolveGameIdAsync(DbConnection connection, string slug, CancellationToken ct)
    {
        var id = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM games WHERE slug = @Slug AND status = 'published'", new { Slug = slug }, cancellationToken: ct));
        return id ?? throw new NotFoundException("Game", slug);
    }

    private static async Task EnsureNotBlockedAsync(DbConnection connection, Guid a, Guid b, CancellationToken ct)
    {
        var blocked = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(
              SELECT 1 FROM user_blocks
              WHERE (blocker_id = @A AND blocked_id = @B) OR (blocker_id = @B AND blocked_id = @A)
            )
            """, new { A = a, B = b }, cancellationToken: ct));
        if (blocked) throw new ForbiddenException("Action not allowed for blocked users.");
    }

    private async Task<DbConnection> OpenAsync(CancellationToken ct)
    {
        var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(ct);
        return connection;
    }

    private static CommunityCommentDto MapComment(dynamic r, IReadOnlyList<CommunityCommentDto> replies) => new(
        (Guid)r.id, (Guid)r.post_id, (Guid?)r.parent_id, (string)r.content, (int)r.reaction_count,
        (DateTimeOffset)r.created_at,
        new CommunityUserDto((Guid)r.user_id, (string)r.username, (string?)r.display_name, (string?)r.avatar_url),
        replies, (string?)r.viewer_reaction);

    private static string BuildActivityMessage(string type, string username, string? gameTitle) => type switch
    {
        "game_shared" => $"{username} shared {gameTitle ?? "a game"}",
        "game_reviewed" => $"{username} reviewed {gameTitle ?? "a game"}",
        "discussion_created" => $"{username} started a discussion{(gameTitle is null ? "" : $" about {gameTitle}")}",
        "comment_created" => $"{username} commented{(gameTitle is null ? "" : $" on {gameTitle}")}",
        "achievement_unlocked" => $"{username} unlocked an achievement",
        "favorite_added" => $"{username} favorited {gameTitle ?? "a game"}",
        _ => $"{username} was active in the community"
    };

    private static string BuildNotificationMessage(string type, string? actor) => type switch
    {
        "comment_on_post" => $"{actor ?? "Someone"} commented on your post",
        "reply_to_comment" => $"{actor ?? "Someone"} replied to your comment",
        "user_followed" => $"{actor ?? "Someone"} followed you",
        "achievement_unlocked" => "You unlocked a new achievement",
        "post_created" => $"{actor ?? "Someone"} you follow created a post",
        _ => "You have a new notification"
    };
}

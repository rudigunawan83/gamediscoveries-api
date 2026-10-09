using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Community.Domain;
using GameDiscoveries.Modules.Community.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Community.Features;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/community/home", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<CommunityHomeDto>.Ok(await svc.GetHomeAsync(TryUser(user), ct))))
            .WithTags("Community").AllowAnonymous().RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/community/feed", async (string? cursor, int? limit, string? sort, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            var (items, next) = await svc.GetFeedAsync(TryUser(user), cursor, limit ?? 20, sort ?? "newest", ct);
            return Results.Ok(ApiResponse<object>.Ok(new { items, nextCursor = next }));
        }).WithTags("Community").AllowAnonymous().RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/community/posts", async (string? sort, string? q, string? cursor, int? limit, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            var (items, next) = await svc.ListPostsAsync(TryUser(user), sort ?? "latest", q, cursor, limit ?? 20, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { items, nextCursor = next }));
        }).WithTags("Community").AllowAnonymous().RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/community/posts/{id:guid}", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<CommunityPostDto>.Ok(await svc.GetPostAsync(id, TryUser(user), ct))))
            .WithTags("Community").AllowAnonymous();

        endpoints.MapPost("/api/v1/community/posts", async (CreatePostRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<CommunityPostDto>.Ok(await svc.CreatePostAsync(RequireUser(user), req.Type, req.Title, req.Content, req.GameId, ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated).RequireRateLimiting("public");

        endpoints.MapPut("/api/v1/community/posts/{id:guid}", async (Guid id, UpdatePostRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<CommunityPostDto>.Ok(await svc.UpdatePostAsync(RequireUser(user), id, req.Title, req.Content, IsMod(user), ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapDelete("/api/v1/community/posts/{id:guid}", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.DeletePostAsync(RequireUser(user), id, IsMod(user), ct);
            return Results.Ok(ApiResponse<object>.Ok(new { deleted = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/community/posts/{id:guid}/comments", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<CommunityCommentDto>>.Ok(await svc.GetCommentsAsync(id, TryUser(user), ct))))
            .WithTags("Community").AllowAnonymous();

        endpoints.MapPost("/api/v1/community/posts/{id:guid}/comments", async (Guid id, CreateCommentRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<CommunityCommentDto>.Ok(await svc.CreateCommentAsync(RequireUser(user), id, req.Content, req.ParentId, ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapDelete("/api/v1/community/comments/{id:guid}", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.DeleteCommentAsync(RequireUser(user), id, IsMod(user), ct);
            return Results.Ok(ApiResponse<object>.Ok(new { deleted = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapPost("/api/v1/community/{targetType}/{targetId:guid}/reactions", async (string targetType, Guid targetId, ReactionRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.SetReactionAsync(RequireUser(user), targetType, targetId, req.Reaction, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { reacted = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapDelete("/api/v1/community/{targetType}/{targetId:guid}/reactions", async (string targetType, Guid targetId, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.RemoveReactionAsync(RequireUser(user), targetType, targetId, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { removed = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/games/{slug}/community", async (string slug, string? sort, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<CommunityPostDto>>.Ok(
                await svc.GetGameDiscussionsAsync(slug, sort ?? "latest", TryUser(user), ct))))
            .WithTags("Community").AllowAnonymous().RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/games/{slug}/reviews", async (string slug, ICommunityService svc, CancellationToken ct) =>
        {
            var (summary, items) = await svc.GetReviewsAsync(slug, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { summary, items }));
        }).WithTags("Community").AllowAnonymous();

        endpoints.MapPost("/api/v1/games/{slug}/reviews", async (string slug, UpsertReviewRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<GameReviewDto>.Ok(await svc.UpsertReviewAsync(RequireUser(user), slug, req.Rating, req.Content, ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapDelete("/api/v1/reviews/{id:guid}", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.DeleteReviewAsync(RequireUser(user), id, IsMod(user), ct);
            return Results.Ok(ApiResponse<object>.Ok(new { deleted = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/users/me/reviews", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<MyReviewDto>>.Ok(await svc.GetMyReviewsAsync(RequireUser(user), ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/users/me/privacy", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<PrivacySettingsDto>.Ok(await svc.GetPrivacyAsync(RequireUser(user), ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/users/{username}/profile", async (string username, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<UserProfileDto>.Ok(await svc.GetProfileAsync(username, TryUser(user), ct))))
            .WithTags("Community").AllowAnonymous();

        endpoints.MapPut("/api/v1/users/me/privacy", async (PrivacyRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.UpdatePrivacyAsync(RequireUser(user), req.ShowFavorites, req.ShowHistory, req.ShowAchievements, req.ShowActivity, req.ShowOnLeaderboards, req.Bio, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { updated = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapPost("/api/v1/users/{id:guid}/follow", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.FollowAsync(RequireUser(user), id, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { following = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapDelete("/api/v1/users/{id:guid}/follow", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.UnfollowAsync(RequireUser(user), id, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { following = false }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapPost("/api/v1/users/{id:guid}/block", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.BlockAsync(RequireUser(user), id, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { blocked = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapDelete("/api/v1/users/{id:guid}/block", async (Guid id, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.UnblockAsync(RequireUser(user), id, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { blocked = false }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/community/achievements", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<AchievementDto>>.Ok(await svc.GetAchievementsAsync(TryUser(user), ct))))
            .WithTags("Community").AllowAnonymous();

        endpoints.MapGet("/api/v1/users/me/achievements", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<AchievementDto>>.Ok(await svc.GetAchievementsAsync(RequireUser(user), ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/community/challenges", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<ChallengeDto>>.Ok(await svc.GetChallengesAsync(TryUser(user), ct))))
            .WithTags("Community").AllowAnonymous();

        endpoints.MapGet("/api/v1/users/me/challenges", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<ChallengeDto>>.Ok(await svc.GetChallengesAsync(RequireUser(user), ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/community/leaderboards", async (string? type, string? period, ICommunityService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<LeaderboardEntryDto>>.Ok(await svc.GetLeaderboardAsync(type ?? "players", period ?? "weekly", ct))))
            .WithTags("Community").AllowAnonymous();

        endpoints.MapGet("/api/v1/community/notifications", async (ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            var (items, unread) = await svc.GetNotificationsAsync(RequireUser(user), ct);
            return Results.Ok(ApiResponse<object>.Ok(new { items, unread }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapPost("/api/v1/community/notifications/read", async (ReadNotificationsRequest? req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.MarkNotificationsReadAsync(RequireUser(user), req?.NotificationId, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { read = true }));
        }).WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapPost("/api/v1/community/reports", async (CreateReportRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(ApiResponse<CommunityReportDto>.Ok(await svc.CreateReportAsync(RequireUser(user), req.TargetType, req.TargetId, req.Reason, req.Description, ct))))
            .WithTags("Community").RequireAuthorization(Policies.Authenticated);

        endpoints.MapGet("/api/v1/admin/community/reports", async (string? status, ICommunityService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<CommunityReportDto>>.Ok(await svc.ListReportsAsync(status, ct))))
            .WithTags("Administration").RequireAuthorization(Policies.ModeratorOrAdmin);

        endpoints.MapPost("/api/v1/admin/community/moderate", async (ModerateRequest req, ICommunityService svc, ICurrentUser user, CancellationToken ct) =>
        {
            await svc.ModerateAsync(RequireUser(user), req.Action, req.TargetType, req.TargetId, req.Resolution, ct);
            return Results.Ok(ApiResponse<object>.Ok(new { moderated = true }));
        }).WithTags("Administration").RequireAuthorization(Policies.ModeratorOrAdmin);

        endpoints.MapGet("/api/v1/community/signals/games/{gameId:guid}", async (Guid gameId, ICommunityGameSignalsService signals, CancellationToken ct) =>
            Results.Ok(ApiResponse<object>.Ok(await signals.GetCommunitySignalsAsync(gameId, ct))))
            .WithTags("Community").AllowAnonymous();

        return endpoints;
    }

    private static Guid? TryUser(ICurrentUser user)
        => user.IsAuthenticated && Guid.TryParse(user.UserId, out var id) ? id : null;

    private static Guid RequireUser(ICurrentUser user)
        => TryUser(user) ?? throw new UnauthorizedException();

    private static bool IsMod(ICurrentUser user)
        => user.Roles.Any(r => r is "Moderator" or "Admin" or "SuperAdmin");
}

public sealed record CreatePostRequest(string Type, string Title, string Content, Guid? GameId);
public sealed record UpdatePostRequest(string Title, string Content);
public sealed record CreateCommentRequest(string Content, Guid? ParentId);
public sealed record ReactionRequest(string Reaction);
public sealed record UpsertReviewRequest(int Rating, string Content);
public sealed record PrivacyRequest(bool? ShowFavorites, bool? ShowHistory, bool? ShowAchievements, bool? ShowActivity, bool? ShowOnLeaderboards, string? Bio);
public sealed record CreateReportRequest(string TargetType, Guid TargetId, string Reason, string? Description);
public sealed record ReadNotificationsRequest(Guid? NotificationId);
public sealed record ModerateRequest(string Action, string TargetType, Guid TargetId, string? Resolution);

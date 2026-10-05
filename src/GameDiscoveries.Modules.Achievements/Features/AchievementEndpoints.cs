using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Achievements.Models;
using GameDiscoveries.Modules.Achievements.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Achievements.Features;

public static class AchievementEndpoints
{
    public static IEndpointRouteBuilder MapAchievementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/achievements", async (
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await achievements.GetUserAchievementsAsync(userId, null, cancellationToken);
                return Results.Ok(ApiResponse<AchievementListResponse>.Ok(result));
            })
            .WithName("GetMyAchievements")
            .WithTags("Achievements")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/me/achievements/unlocked", async (
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await achievements.GetUserAchievementsAsync(userId, "unlocked", cancellationToken);
                return Results.Ok(ApiResponse<AchievementListResponse>.Ok(result));
            })
            .WithName("GetMyUnlockedAchievements")
            .WithTags("Achievements")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/me/achievements/in-progress", async (
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await achievements.GetUserAchievementsAsync(userId, "in-progress", cancellationToken);
                return Results.Ok(ApiResponse<AchievementListResponse>.Ok(result));
            })
            .WithName("GetMyInProgressAchievements")
            .WithTags("Achievements")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/me/achievements/recent", async (
                int? limit,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await achievements.GetRecentAsync(userId, limit ?? 10, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<AchievementHistoryDto>>.Ok(result));
            })
            .WithName("GetMyRecentAchievements")
            .WithTags("Achievements")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/me/achievements/{code}", async (
                string code,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await achievements.GetByCodeAsync(userId, code, cancellationToken);
                return Results.Ok(ApiResponse<AchievementDto>.Ok(result));
            })
            .WithName("GetMyAchievementByCode")
            .WithTags("Achievements")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/gamification/achievements", async (
                IAchievementService achievements,
                CancellationToken cancellationToken) =>
            {
                var result = await achievements.GetAdminDefinitionsAsync(cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { result.Items, result.Overview }));
            })
            .WithName("AdminListAchievements")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/gamification/achievements/overview", async (
                IAchievementService achievements,
                CancellationToken cancellationToken) =>
            {
                var result = await achievements.GetAdminDefinitionsAsync(cancellationToken);
                return Results.Ok(ApiResponse<AdminAchievementOverviewStats>.Ok(result.Overview));
            })
            .WithName("AdminAchievementOverview")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/gamification/achievements", async (
                UpsertAchievementRequest request,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var result = await achievements.CreateAsync(adminId, request, cancellationToken);
                return Results.Ok(ApiResponse<AdminAchievementDefinitionDto>.Ok(result));
            })
            .WithName("AdminCreateAchievement")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/gamification/achievements/{id:guid}", async (
                Guid id,
                IAchievementService achievements,
                CancellationToken cancellationToken) =>
            {
                var result = await achievements.GetAdminDefinitionAsync(id, cancellationToken);
                return Results.Ok(ApiResponse<AdminAchievementDefinitionDto>.Ok(result));
            })
            .WithName("AdminGetAchievement")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPut("/api/v1/admin/gamification/achievements/{id:guid}", async (
                Guid id,
                UpsertAchievementRequest request,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var result = await achievements.UpdateAsync(adminId, id, request, cancellationToken);
                return Results.Ok(ApiResponse<AdminAchievementDefinitionDto>.Ok(result));
            })
            .WithName("AdminUpdateAchievement")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/gamification/achievements/{id:guid}/activate", async (
                Guid id,
                AdminAchievementActionRequest request,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await achievements.SetActiveAsync(adminId, id, true, request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { id, isActive = true }));
            })
            .WithName("AdminActivateAchievement")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/gamification/achievements/{id:guid}/deactivate", async (
                Guid id,
                AdminAchievementActionRequest request,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await achievements.SetActiveAsync(adminId, id, false, request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { id, isActive = false }));
            })
            .WithName("AdminDeactivateAchievement")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/gamification/achievements/{id:guid}/users", async (
                Guid id,
                IAchievementService achievements,
                CancellationToken cancellationToken) =>
            {
                var result = await achievements.GetUsersAsync(id, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<AdminAchievementUserDto>>.Ok(result));
            })
            .WithName("AdminAchievementUsers")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/users/{userId:guid}/achievements", async (
                Guid userId,
                IAchievementService achievements,
                CancellationToken cancellationToken) =>
            {
                var result = await achievements.GetAdminUserAchievementsAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<AchievementDto>>.Ok(result));
            })
            .WithName("AdminGetUserAchievements")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/achievements/{achievementId:guid}/grant", async (
                Guid userId,
                Guid achievementId,
                AdminAchievementActionRequest request,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await achievements.GrantAsync(adminId, userId, achievementId, request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { userId, achievementId, granted = true }));
            })
            .WithName("AdminGrantAchievement")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/achievements/{achievementId:guid}/revoke", async (
                Guid userId,
                Guid achievementId,
                AdminAchievementActionRequest request,
                IAchievementService achievements,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await achievements.RevokeAsync(adminId, userId, achievementId, request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { userId, achievementId, revoked = true }));
            })
            .WithName("AdminRevokeAchievement")
            .WithTags("Administration", "Achievements")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public");

        return endpoints;
    }

    private static Guid RequireUser(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        return userId;
    }
}

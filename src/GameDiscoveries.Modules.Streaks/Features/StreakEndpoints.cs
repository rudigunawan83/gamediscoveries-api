using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Streaks.Models;
using GameDiscoveries.Modules.Streaks.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Streaks.Features;

public static class StreakEndpoints
{
    public static IEndpointRouteBuilder MapStreakEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/streak", async (
                IStreakService streaks,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await streaks.GetCurrentAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<StreakStatusDto>.Ok(result));
            })
            .WithName("GetMyStreak")
            .WithTags("Streaks")
            .WithSummary("Get current user streak status.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/me/streak/history", async (
                int? page,
                int? pageSize,
                string? eventType,
                DateOnly? dateFrom,
                DateOnly? dateTo,
                IStreakService streaks,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await streaks.GetHistoryAsync(
                    userId, page ?? 1, pageSize ?? 20, eventType, dateFrom, dateTo, cancellationToken);
                return Results.Ok(ApiResponse<PagedStreakHistoryDto>.Ok(result));
            })
            .WithName("GetMyStreakHistory")
            .WithTags("Streaks")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/gamification/streaks", async (
                IStreakService streaks,
                CancellationToken cancellationToken) =>
            {
                var overview = await streaks.GetOverviewAsync(cancellationToken);
                return Results.Ok(ApiResponse<AdminStreakOverviewDto>.Ok(overview));
            })
            .WithName("AdminStreakOverview")
            .WithTags("Administration", "Streaks")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/users/{userId:guid}/streak", async (
                Guid userId,
                IStreakService streaks,
                CancellationToken cancellationToken) =>
            {
                var detail = await streaks.GetAdminUserStreakAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<AdminUserStreakDto>.Ok(detail));
            })
            .WithName("AdminGetUserStreak")
            .WithTags("Administration", "Streaks")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/streak/freeze", async (
                Guid userId,
                AdminFreezeRequest request,
                IStreakService streaks,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await streaks.GrantFreezeAsync(userId, adminId, request.Amount, request.Reason, cancellationToken);
                var streak = await streaks.GetCurrentAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<StreakStatusDto>.Ok(streak));
            })
            .WithName("AdminGrantStreakFreeze")
            .WithTags("Administration", "Streaks")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/streak/freeze/remove", async (
                Guid userId,
                AdminFreezeRequest request,
                IStreakService streaks,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await streaks.RemoveFreezeAsync(userId, adminId, request.Amount, request.Reason, cancellationToken);
                var streak = await streaks.GetCurrentAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<StreakStatusDto>.Ok(streak));
            })
            .WithName("AdminRemoveStreakFreeze")
            .WithTags("Administration", "Streaks")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/streak/reset", async (
                Guid userId,
                AdminStreakResetRequest request,
                IStreakService streaks,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await streaks.ResetStreakAsync(userId, adminId, request.Reason, cancellationToken);
                var streak = await streaks.GetCurrentAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<StreakStatusDto>.Ok(streak));
            })
            .WithName("AdminResetStreakV2")
            .WithTags("Administration", "Streaks")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

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

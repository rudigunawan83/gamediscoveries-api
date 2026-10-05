using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Xp.Features;

public static class XpEndpoints
{
    public static IEndpointRouteBuilder MapXpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/progress", async (
                IProgressService progress,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await progress.GetMyProgressAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<UserProgressResponse>.Ok(result));
            })
            .WithName("GetMyProgress")
            .WithTags("Progress", "Xp")
            .WithSummary("Get current user level, XP progress, and stats.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/me/xp", async (
                IXpEngine xp,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var summary = await xp.GetSummaryAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<UserXpSummary>.Ok(summary));
            })
            .WithName("GetMyXp")
            .WithTags("Xp")
            .WithSummary("Get current user XP summary.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/me/xp/transactions", async (
                int? page,
                int? pageSize,
                int? limit,
                int? offset,
                string? ruleCode,
                DateTimeOffset? dateFrom,
                DateTimeOffset? dateTo,
                DateTimeOffset? from,
                DateTimeOffset? to,
                IProgressService progress,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var resolvedPageSize = pageSize ?? limit ?? 20;
                var resolvedPage = page ?? ((offset ?? 0) / Math.Max(resolvedPageSize, 1)) + 1;
                var result = await progress.GetMyXpTransactionsAsync(
                    userId,
                    resolvedPage,
                    resolvedPageSize,
                    ruleCode,
                    dateFrom ?? from,
                    dateTo ?? to,
                    cancellationToken);
                return Results.Ok(ApiResponse<PagedXpTransactionsResponse>.Ok(result));
            })
            .WithName("GetMyXpTransactions")
            .WithTags("Xp", "Progress")
            .WithSummary("Get current user XP transaction history (paged).")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        // ---- Admin gamification ----

        endpoints.MapGet("/api/v1/admin/gamification/overview", async (
                IAdminGamificationService admin,
                CancellationToken cancellationToken) =>
            {
                var overview = await admin.GetOverviewAsync(cancellationToken);
                return Results.Ok(ApiResponse<GamificationOverviewResponse>.Ok(overview));
            })
            .WithName("AdminGamificationOverview")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/users", async (
                string? search,
                int? level,
                string? status,
                string? sort,
                int? page,
                int? pageSize,
                IAdminGamificationService admin,
                CancellationToken cancellationToken) =>
            {
                var (items, total) = await admin.ListUsersAsync(
                    search, level, status, sort, page ?? 1, pageSize ?? 20, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    items,
                    page = page ?? 1,
                    pageSize = pageSize ?? 20,
                    total
                }));
            })
            .WithName("AdminListUsers")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/users/{userId:guid}", async (
                Guid userId,
                IAdminGamificationService admin,
                CancellationToken cancellationToken) =>
            {
                var detail = await admin.GetUserDetailAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<AdminUserDetailResponse>.Ok(detail));
            })
            .WithName("AdminGetUser")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/users/{userId:guid}/xp", async (
                Guid userId,
                IXpEngine xp,
                CancellationToken cancellationToken) =>
            {
                var summary = await xp.GetSummaryAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<UserXpSummary>.Ok(summary));
            })
            .WithName("AdminGetUserXp")
            .WithTags("Administration", "Xp")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/users/{userId:guid}/xp/transactions", async (
                Guid userId,
                int? page,
                int? pageSize,
                string? ruleCode,
                IProgressService progress,
                CancellationToken cancellationToken) =>
            {
                var result = await progress.GetMyXpTransactionsAsync(
                    userId, page ?? 1, pageSize ?? 50, ruleCode, null, null, cancellationToken);
                return Results.Ok(ApiResponse<PagedXpTransactionsResponse>.Ok(result));
            })
            .WithName("AdminGetUserXpTransactions")
            .WithTags("Administration", "Xp")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/xp-adjustments", async (
                Guid userId,
                AdminXpAdjustmentRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var result = await admin.AdjustXpAsync(
                    userId, adminId, request.ResolvedAmount, request.ResolvedReason, cancellationToken);
                return Results.Ok(ApiResponse<XpAwardResult>.Ok(result));
            })
            .WithName("AdminAdjustUserXpV2")
            .WithTags("Administration", "Xp")
            .WithSummary("Create an audited admin XP adjustment.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        // Legacy path retained for Phase 03 clients
        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/xp/adjust", async (
                Guid userId,
                AdminXpAdjustmentRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var result = await admin.AdjustXpAsync(
                    userId, adminId, request.ResolvedAmount, request.ResolvedReason, cancellationToken);
                return Results.Ok(ApiResponse<XpAwardResult>.Ok(result));
            })
            .WithName("AdminAdjustUserXp")
            .WithTags("Administration", "Xp")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/xp/transactions/{transactionId:guid}/reverse", async (
                Guid transactionId,
                ReverseXpRequest request,
                IXpEngine xp,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var result = await xp.ReverseAsync(transactionId, adminId, request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<XpAwardResult>.Ok(result));
            })
            .WithName("AdminReverseXpTransaction")
            .WithTags("Administration", "Xp")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/gamification/reset", async (
                Guid userId,
                AdminReasonRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.ResetGamificationAsync(userId, adminId, request.Reason ?? "reset", cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { reset = true }));
            })
            .WithName("AdminResetGamification")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/streak/reset", async (
                Guid userId,
                AdminReasonRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.ResetStreakAsync(userId, adminId, request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { reset = true }));
            })
            .WithName("AdminResetStreak")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/suspend", async (
                Guid userId,
                AdminSuspendRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.SetUserStatusAsync(userId, adminId, "suspended", request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { status = "suspended" }));
            })
            .WithName("AdminSuspendUser")
            .WithTags("Administration")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/users/{userId:guid}/unsuspend", async (
                Guid userId,
                AdminSuspendRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.SetUserStatusAsync(userId, adminId, "active", request.Reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { status = "active" }));
            })
            .WithName("AdminUnsuspendUser")
            .WithTags("Administration")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/gamification/levels", async (
                bool? includeInactive,
                IAdminGamificationService admin,
                CancellationToken cancellationToken) =>
            {
                var levels = await admin.ListLevelsAsync(includeInactive ?? true, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { items = levels }));
            })
            .WithName("AdminListLevels")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/gamification/levels", async (
                UpsertLevelRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var level = await admin.CreateLevelAsync(adminId, request, cancellationToken);
                return Results.Ok(ApiResponse<LevelDefinitionDto>.Ok(level));
            })
            .WithName("AdminCreateLevel")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPut("/api/v1/admin/gamification/levels/{level:int}", async (
                int level,
                UpsertLevelRequest request,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var updated = await admin.UpdateLevelAsync(adminId, level, request with { Level = level }, cancellationToken);
                return Results.Ok(ApiResponse<LevelDefinitionDto>.Ok(updated));
            })
            .WithName("AdminUpdateLevel")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/gamification/levels/{level:int}/activate", async (
                int level,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.SetLevelActiveAsync(adminId, level, true, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { level, isActive = true }));
            })
            .WithName("AdminActivateLevel")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/gamification/levels/{level:int}/deactivate", async (
                int level,
                IAdminGamificationService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.SetLevelActiveAsync(adminId, level, false, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { level, isActive = false }));
            })
            .WithName("AdminDeactivateLevel")
            .WithTags("Administration", "Gamification")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/audit-logs", async (
                int? limit,
                int? offset,
                string? action,
                string? targetType,
                Guid? adminId,
                IAuditLogService audit,
                CancellationToken cancellationToken) =>
            {
                var items = await audit.ListAsync(
                    limit ?? 50, offset ?? 0, action, targetType, adminId, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { items }));
            })
            .WithName("AdminListAuditLogs")
            .WithTags("Administration")
            .RequireAuthorization(Policies.AdminOnly)
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

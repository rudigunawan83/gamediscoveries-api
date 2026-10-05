using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Missions.Models;
using GameDiscoveries.Modules.Missions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Missions.Features;

public static class MissionEndpoints
{
    public static IEndpointRouteBuilder MapMissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/missions", async (
                IMissionService missions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await missions.GetMyMissionsAsync(userId, cancellationToken);
                return Results.Ok(ApiResponse<MyMissionsResponse>.Ok(result));
            })
            .WithName("GetMyMissions")
            .WithTags("Missions")
            .WithSummary("Get current daily missions and weekly challenges.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/me/missions/history", async (
                int? page,
                int? pageSize,
                string? type,
                string? status,
                DateTimeOffset? dateFrom,
                DateTimeOffset? dateTo,
                IMissionService missions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var result = await missions.GetHistoryAsync(
                    userId, page ?? 1, pageSize ?? 20, type, status, dateFrom, dateTo, cancellationToken);
                return Results.Ok(ApiResponse<PagedMissionsResponse>.Ok(result));
            })
            .WithName("GetMyMissionHistory")
            .WithTags("Missions")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/me/missions/{missionId:guid}", async (
                Guid missionId,
                IMissionService missions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUser(currentUser);
                var mission = await missions.GetMissionAsync(userId, missionId, cancellationToken);
                if (mission is null)
                {
                    throw new NotFoundException("Mission", "Mission not found.");
                }

                return Results.Ok(ApiResponse<MissionDto>.Ok(mission));
            })
            .WithName("GetMyMissionDetail")
            .WithTags("Missions")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        // Admin
        endpoints.MapGet("/api/v1/admin/gamification/missions/templates", async (
                IAdminMissionService admin,
                CancellationToken cancellationToken) =>
            {
                var items = await admin.ListTemplatesAsync(cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { items }));
            })
            .WithName("AdminListMissionTemplates")
            .WithTags("Administration", "Missions")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/gamification/missions/templates", async (
                UpsertMissionTemplateRequest request,
                IAdminMissionService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var created = await admin.CreateAsync(adminId, request, cancellationToken);
                return Results.Ok(ApiResponse<MissionTemplateDto>.Ok(created));
            })
            .WithName("AdminCreateMissionTemplate")
            .WithTags("Administration", "Missions")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPut("/api/v1/admin/gamification/missions/templates/{templateId:guid}", async (
                Guid templateId,
                UpsertMissionTemplateRequest request,
                IAdminMissionService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var updated = await admin.UpdateAsync(adminId, templateId, request, cancellationToken);
                return Results.Ok(ApiResponse<MissionTemplateDto>.Ok(updated));
            })
            .WithName("AdminUpdateMissionTemplate")
            .WithTags("Administration", "Missions")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/gamification/missions/templates/{templateId:guid}/activate", async (
                Guid templateId,
                IAdminMissionService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.SetActiveAsync(adminId, templateId, true, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { templateId, isActive = true }));
            })
            .WithName("AdminActivateMissionTemplate")
            .WithTags("Administration", "Missions")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/admin/gamification/missions/templates/{templateId:guid}/deactivate", async (
                Guid templateId,
                IAdminMissionService admin,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                await admin.SetActiveAsync(adminId, templateId, false, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { templateId, isActive = false }));
            })
            .WithName("AdminDeactivateMissionTemplate")
            .WithTags("Administration", "Missions")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/gamification/missions/analytics", async (
                IAdminMissionService admin,
                CancellationToken cancellationToken) =>
            {
                var analytics = await admin.GetAnalyticsAsync(cancellationToken);
                return Results.Ok(ApiResponse<MissionAnalyticsResponse>.Ok(analytics));
            })
            .WithName("AdminMissionAnalytics")
            .WithTags("Administration", "Missions")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
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

using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Leaderboards.Domain;
using GameDiscoveries.Modules.Leaderboards.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Leaderboards.Features;

public static class LeaderboardEndpoints
{
    public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/leaderboards", async (
                ILeaderboardService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await service.ListAsync(cancellationToken);
                    return Results.Ok(ApiResponse<IReadOnlyList<LeaderboardListItemDto>>.Ok(result));
                }
                catch (Exception)
                {
                    return Results.Json(
                        ApiResponse<object>.Fail(new ApiErrorPayload
                        {
                            Type = "LEADERBOARD_UNAVAILABLE",
                            Title = "Leaderboard unavailable",
                            Status = StatusCodes.Status503ServiceUnavailable,
                            Detail = "Leaderboard is temporarily unavailable."
                        }),
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .WithName("ListLeaderboards")
            .WithTags("Leaderboards")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/leaderboards/{code}", async (
                string code,
                int? limit,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var userId = TryGetUserId(currentUser);
                    var result = await service.GetAsync(code, limit, userId, cancellationToken);
                    return Results.Ok(ApiResponse<LeaderboardDetailResponse>.Ok(result));
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound(ApiResponse<object>.Fail(new ApiErrorPayload
                    {
                        Type = "NOT_FOUND",
                        Title = "Not found",
                        Status = StatusCodes.Status404NotFound,
                        Detail = "Leaderboard not found."
                    }));
                }
                catch (Exception)
                {
                    return Results.Json(
                        ApiResponse<object>.Fail(new ApiErrorPayload
                        {
                            Type = "LEADERBOARD_UNAVAILABLE",
                            Title = "Leaderboard unavailable",
                            Status = StatusCodes.Status503ServiceUnavailable,
                            Detail = "Leaderboard is temporarily unavailable."
                        }),
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .WithName("GetLeaderboard")
            .WithTags("Leaderboards")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/leaderboards/{code}/me", async (
                string code,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUserId(currentUser);
                var result = await service.GetMeAsync(code, userId, cancellationToken);
                return Results.Ok(ApiResponse<UserRankResponse>.Ok(result));
            })
            .WithName("GetMyLeaderboardRank")
            .WithTags("Leaderboards")
            .RequireAuthorization()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/leaderboards/{code}/history", async (
                string code,
                int? limit,
                ILeaderboardService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetHistoryAsync(code, limit ?? 20, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<LeaderboardHistoryItemDto>>.Ok(result));
            })
            .WithName("GetLeaderboardHistory")
            .WithTags("Leaderboards")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/me/leaderboards/history", async (
                int? limit,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUserId(currentUser);
                var result = await service.GetMyHistoryAsync(userId, limit ?? 20, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<LeaderboardHistoryItemDto>>.Ok(result));
            })
            .WithName("GetMyLeaderboardHistory")
            .WithTags("Leaderboards")
            .RequireAuthorization()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/competitions", async (
                ILeaderboardService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.ListCompetitionsAsync(cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<CompetitionDto>>.Ok(result));
            })
            .WithName("ListCompetitions")
            .WithTags("Competitions")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/competitions/{code}/join", async (
                string code,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = RequireUserId(currentUser);
                await service.JoinCompetitionAsync(code, userId, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { joined = true }));
            })
            .WithName("JoinCompetition")
            .WithTags("Competitions")
            .RequireAuthorization()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/leaderboards", async (
                ILeaderboardService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.AdminOverviewAsync(cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<AdminLeaderboardOverviewDto>>.Ok(result));
            })
            .WithName("AdminLeaderboardsOverview")
            .WithTags("Administration", "Leaderboards")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/leaderboards/{code}/rebuild", async (
                string code,
                AdminLeaderboardActionRequest request,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUserId(currentUser);
                var reason = string.IsNullOrWhiteSpace(request.Reason) ? "rebuild" : request.Reason.Trim();
                await service.RebuildAsync(code, adminId, reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { rebuilt = true }));
            })
            .WithName("AdminRebuildLeaderboard")
            .WithTags("Administration", "Leaderboards")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/leaderboards/{code}/users/{userId:guid}/disqualify", async (
                string code,
                Guid userId,
                AdminLeaderboardActionRequest request,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUserId(currentUser);
                var reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? throw new ValidationException("Reason is required.")
                    : request.Reason.Trim();
                await service.DisqualifyAsync(code, userId, true, adminId, reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { disqualified = true }));
            })
            .WithName("AdminDisqualifyLeaderboardUser")
            .WithTags("Administration", "Leaderboards")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/leaderboards/{code}/users/{userId:guid}/restore", async (
                string code,
                Guid userId,
                AdminLeaderboardActionRequest request,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUserId(currentUser);
                var reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? throw new ValidationException("Reason is required.")
                    : request.Reason.Trim();
                await service.DisqualifyAsync(code, userId, false, adminId, reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { restored = true }));
            })
            .WithName("AdminRestoreLeaderboardUser")
            .WithTags("Administration", "Leaderboards")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/competitions/{code}/settle", async (
                string code,
                AdminLeaderboardActionRequest request,
                ILeaderboardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUserId(currentUser);
                var reason = string.IsNullOrWhiteSpace(request.Reason) ? "settle" : request.Reason.Trim();
                await service.SettleCompetitionAsync(code, adminId, reason, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { settled = true }));
            })
            .WithName("AdminSettleCompetition")
            .WithTags("Administration", "Competitions")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public");

        return endpoints;
    }

    private static Guid? TryGetUserId(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated && Guid.TryParse(currentUser.UserId, out var id) ? id : null;

    private static Guid RequireUserId(ICurrentUser currentUser) =>
        TryGetUserId(currentUser) ?? throw new UnauthorizedAccessException("Authentication required.");
}

public sealed record AdminLeaderboardActionRequest(string? Reason);

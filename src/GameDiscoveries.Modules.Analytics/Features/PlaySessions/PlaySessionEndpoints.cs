using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Analytics.Features.PlaySessions;

public static class PlaySessionEndpoints
{
    public static IEndpointRouteBuilder MapPlaySessions(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/games/{gameId:guid}/sessions/start", async (
                Guid gameId,
                StartGamePlaySessionRequest request,
                IGamePlaySessionService sessions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sessions.StartAsync(
                    gameId,
                    request,
                    ResolveUserId(currentUser),
                    cancellationToken);
                return Results.Ok(ApiResponse<GamePlaySessionResponse>.Ok(result));
            })
            .WithName("StartGamePlaySession")
            .WithTags("GameSessions")
            .WithSummary("Start a tracked game play session.")
            .WithDescription(
                "Creates a game_play_sessions row and emits GAME_START + GAME_SESSION_START. " +
                "UserId is resolved from JWT. Duplicate sessionId is idempotent.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/api/v1/games/sessions/{sessionId}/heartbeat", async (
                string sessionId,
                HeartbeatGamePlaySessionRequest request,
                IGamePlaySessionService sessions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sessions.HeartbeatAsync(
                    sessionId,
                    request,
                    ResolveUserId(currentUser),
                    request.AnonymousId,
                    cancellationToken);
                return Results.Ok(ApiResponse<GamePlaySessionResponse>.Ok(result));
            })
            .WithName("HeartbeatGamePlaySession")
            .WithTags("GameSessions")
            .WithSummary("Heartbeat proving the player is still active.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/v1/games/sessions/{sessionId}/pause", async (
                string sessionId,
                PauseGamePlaySessionRequest request,
                IGamePlaySessionService sessions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sessions.PauseAsync(
                    sessionId,
                    request,
                    ResolveUserId(currentUser),
                    request.AnonymousId,
                    cancellationToken);
                return Results.Ok(ApiResponse<GamePlaySessionResponse>.Ok(result));
            })
            .WithName("PauseGamePlaySession")
            .WithTags("GameSessions")
            .WithSummary("Pause an active game play session.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/games/sessions/{sessionId}/resume", async (
                string sessionId,
                ResumeGamePlaySessionRequest request,
                IGamePlaySessionService sessions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sessions.ResumeAsync(
                    sessionId,
                    request,
                    ResolveUserId(currentUser),
                    request.AnonymousId,
                    cancellationToken);
                return Results.Ok(ApiResponse<GamePlaySessionResponse>.Ok(result));
            })
            .WithName("ResumeGamePlaySession")
            .WithTags("GameSessions")
            .WithSummary("Resume a paused game play session.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/api/v1/games/sessions/{sessionId}/end", async (
                string sessionId,
                EndGamePlaySessionRequest request,
                IGamePlaySessionService sessions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sessions.EndAsync(
                    sessionId,
                    request,
                    ResolveUserId(currentUser),
                    request.AnonymousId,
                    cancellationToken);
                return Results.Ok(ApiResponse<GamePlaySessionResponse>.Ok(result));
            })
            .WithName("EndGamePlaySession")
            .WithTags("GameSessions")
            .WithSummary("End a game play session and compute validity.")
            .WithDescription(
                "Server calculates duration/activeSeconds. Client-provided durations are ignored. " +
                "Duplicate end is idempotent.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/games/sessions/{sessionId}", async (
                string sessionId,
                Guid? anonymousId,
                IGamePlaySessionService sessions,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sessions.GetAsync(
                    sessionId,
                    ResolveUserId(currentUser),
                    anonymousId,
                    cancellationToken);
                return Results.Ok(ApiResponse<GamePlaySessionResponse>.Ok(result));
            })
            .WithName("GetGamePlaySession")
            .WithTags("GameSessions")
            .WithSummary("Get a game play session by sessionId.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/v1/admin/analytics/sessions/overview", async (
                IGamePlaySessionService sessions,
                CancellationToken cancellationToken) =>
            {
                var overview = await sessions.GetOverviewAsync(cancellationToken);
                return Results.Ok(ApiResponse<GamePlaySessionOverview>.Ok(overview));
            })
            .WithName("GetGamePlaySessionOverview")
            .WithTags("Administration", "GameSessions")
            .WithSummary("Admin overview of game play sessions.")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        return endpoints;
    }

    private static Guid? ResolveUserId(ICurrentUser currentUser)
    {
        if (currentUser.IsAuthenticated && Guid.TryParse(currentUser.UserId, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

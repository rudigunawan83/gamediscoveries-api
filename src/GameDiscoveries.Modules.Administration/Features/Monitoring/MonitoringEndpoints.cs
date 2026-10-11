using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public static class MonitoringEndpoints
{
    public static IEndpointRouteBuilder MapMonitoring(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/monitoring/overview", async (
                string? range,
                bool? refresh,
                IMonitoringService monitoring,
                CancellationToken cancellationToken) =>
            {
                var window = MonitoringWindow.Parse(range, DateTimeOffset.UtcNow);
                var overview = await monitoring.GetOverviewAsync(window, refresh == true, cancellationToken);
                return Results.Ok(ApiResponse<MonitoringOverviewResponse>.Ok(overview));
            })
            .WithName("GetMonitoringOverview")
            .WithTags("Administration", "Monitoring")
            .WithSummary("Read-only platform overview for super admins.")
            .WithDescription(
                "Aggregates play sessions, new accounts, database health, and feed sync status. " +
                "Metrics without a data source are returned as unavailable, never as zero. " +
                "Results are cached for 90 seconds unless refresh=true.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapGet("/api/v1/admin/monitoring/traffic", async (
                string? range,
                bool? refresh,
                ITrafficMonitoringService traffic,
                CancellationToken cancellationToken) =>
            {
                var window = MonitoringWindow.Parse(range, DateTimeOffset.UtcNow);
                var result = await traffic.GetTrafficAsync(window, refresh == true, cancellationToken);
                return Results.Ok(ApiResponse<MonitoringTrafficResponse>.Ok(result));
            })
            .WithName("GetMonitoringTraffic")
            .WithTags("Administration", "Monitoring")
            .WithSummary("Read-only website traffic and growth for super admins.")
            .WithDescription(
                "Aggregates analytics events and sessions: visits, page views, unique visitors, " +
                "session duration, sources, devices, browsers, landing and exit paths, and search keywords. " +
                "Country is unavailable because only hashed IPs are stored. " +
                "Results are cached for 5 minutes unless refresh=true.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapGet("/api/v1/admin/monitoring/games", async (
                string? range,
                bool? refresh,
                IGameAnalyticsService games,
                CancellationToken cancellationToken) =>
            {
                var window = MonitoringWindow.Parse(range, DateTimeOffset.UtcNow);
                var result = await games.GetGamesAsync(window, refresh == true, cancellationToken);
                return Results.Ok(ApiResponse<MonitoringGamesResponse>.Ok(result));
            })
            .WithName("GetMonitoringGames")
            .WithTags("Administration", "Monitoring")
            .WithSummary("Read-only game performance and engagement for super admins.")
            .WithDescription(
                "Aggregates play sessions: plays, unique players, server-measured playtime, playtime " +
                "distribution, top games, plays by category, and new versus returning players. " +
                "Launch success is unavailable because the player does not report load failures. " +
                "Results are cached for 5 minutes unless refresh=true.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}

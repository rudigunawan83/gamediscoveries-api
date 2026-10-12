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
                "distribution, top games, plays by category, and new versus returning players, plus the " +
                "launch funnel (view, play click, load, session, valid session) and games failing to load. " +
                "Results are cached for 5 minutes unless refresh=true.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapGet("/api/v1/admin/monitoring/users", async (
                string? range,
                bool? refresh,
                IUserRetentionService users,
                CancellationToken cancellationToken) =>
            {
                var window = MonitoringWindow.Parse(range, DateTimeOffset.UtcNow);
                var result = await users.GetUsersAsync(window, refresh == true, cancellationToken);
                return Results.Ok(ApiResponse<MonitoringUsersResponse>.Ok(result));
            })
            .WithName("GetMonitoringUsers")
            .WithTags("Administration", "Monitoring")
            .WithSummary("Read-only user activity and retention for super admins.")
            .WithDescription(
                "Aggregates analytics events from the website and the app per device: DAU, WAU, MAU, " +
                "new and returning users, weekly cohorts, D1/D7/D30 retention, engagement, an activity " +
                "heatmap in UTC, top games by players, and new users by acquisition source. " +
                "Results are cached for 10 minutes unless refresh=true.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapGet("/api/v1/admin/monitoring/gamification", async (
                string? range,
                bool? refresh,
                IGamificationAnalyticsService gamification,
                CancellationToken cancellationToken) =>
            {
                var window = MonitoringWindow.Parse(range, DateTimeOffset.UtcNow);
                var result = await gamification.GetGamificationAsync(window, refresh == true, cancellationToken);
                return Results.Ok(ApiResponse<MonitoringGamificationResponse>.Ok(result));
            })
            .WithName("GetMonitoringGamification")
            .WithTags("Administration", "Monitoring")
            .WithSummary("Read-only XP, mission, achievement, and streak analytics for super admins.")
            .WithDescription(
                "Aggregates XP transactions, mission completions, achievement unlocks, levels, and streaks. " +
                "Seeded demo accounts are excluded. Levels and streaks are current snapshots. " +
                "Results are cached for 5 minutes unless refresh=true.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapGet("/api/v1/admin/monitoring/seo", async (
                string? range,
                bool? refresh,
                ISeoMonitoringService seo,
                CancellationToken cancellationToken) =>
            {
                var window = MonitoringWindow.Parse(range, DateTimeOffset.UtcNow);
                var result = await seo.GetSeoAsync(window, refresh == true, cancellationToken);
                return Results.Ok(ApiResponse<MonitoringSeoResponse>.Ok(result));
            })
            .WithName("GetMonitoringSeo")
            .WithTags("Administration", "Monitoring")
            .WithSummary("Read-only Google Search performance, sitemaps, and Core Web Vitals for super admins.")
            .WithDescription(
                "Reads Google Search Console (clicks, impressions, CTR, position, pages, queries, countries, " +
                "devices, sitemaps) with a service account and Core Web Vitals from the Chrome UX Report. " +
                "Dates are Search Console days (Pacific Time) ending yesterday. Sections without access are " +
                "returned as unavailable with a reason. Results are cached for 30 minutes unless refresh=true.")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}

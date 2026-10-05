using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.DiscoveryScore.Domain;
using GameDiscoveries.Modules.DiscoveryScore.Models;
using GameDiscoveries.Modules.DiscoveryScore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.DiscoveryScore.Features;

public static class DiscoveryScoreEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryScoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/discovery", async (
                string? type,
                string? period,
                string? category,
                int? page,
                int? limit,
                ITrendingService trending,
                CancellationToken cancellationToken) =>
            {
                var result = await trending.GetRankingAsync(
                    type ?? DiscoveryRankingTypes.Trending,
                    period ?? "24h",
                    category,
                    page ?? 1,
                    limit ?? 20,
                    cancellationToken);
                return Results.Ok(ApiResponse<DiscoveryRankingResponse>.Ok(result));
            })
            .WithName("GetDiscoveryRanking")
            .WithTags("Discovery")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        foreach (var (path, rankingType) in new (string, string)[]
                 {
                     ("trending", DiscoveryRankingTypes.Trending),
                     ("rising", DiscoveryRankingTypes.Rising),
                     ("popular", DiscoveryRankingTypes.Popular),
                     ("new-trending", DiscoveryRankingTypes.NewTrending),
                     ("most-played", DiscoveryRankingTypes.MostPlayed),
                     ("most-favorited", DiscoveryRankingTypes.MostFavorited),
                     ("most-rated", DiscoveryRankingTypes.MostRated),
                     ("most-reviewed", DiscoveryRankingTypes.MostReviewed)
                 })
        {
            var localType = rankingType;
            endpoints.MapGet($"/api/v1/discovery/{path}", async (
                    string? period,
                    string? category,
                    int? page,
                    int? limit,
                    ITrendingService trending,
                    CancellationToken cancellationToken) =>
                {
                    var result = await trending.GetRankingAsync(
                        localType, period ?? "24h", category, page ?? 1, limit ?? 20, cancellationToken);
                    return Results.Ok(ApiResponse<DiscoveryRankingResponse>.Ok(result));
                })
                .WithName($"GetDiscovery_{path}")
                .WithTags("Discovery")
                .AllowAnonymous()
                .RequireRateLimiting("public");
        }

        endpoints.MapGet("/api/v1/trending", async (
                string? period,
                int? limit,
                ITrendingService trending,
                CancellationToken cancellationToken) =>
            {
                var result = await trending.GetRankingAsync(
                    DiscoveryRankingTypes.Trending, period ?? "24h", null, 1, limit ?? 20, cancellationToken);
                return Results.Ok(ApiResponse<DiscoveryRankingResponse>.Ok(result));
            })
            .WithName("GetTrendingAlias")
            .WithTags("Discovery")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/games/{gameId:guid}/discovery-score", async (
                Guid gameId,
                IDiscoveryScoreService scores,
                CancellationToken cancellationToken) =>
            {
                var result = await scores.GetExplainAsync(gameId, cancellationToken)
                    ?? throw new NotFoundException("DiscoveryScore", gameId.ToString("D"));
                return Results.Ok(ApiResponse<DiscoveryScoreExplainDto>.Ok(result));
            })
            .WithName("GetGameDiscoveryScore")
            .WithTags("Discovery")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/discovery/overview", async (
                IDiscoveryScoreService scores,
                CancellationToken cancellationToken) =>
            {
                var result = await scores.GetOverviewAsync(cancellationToken);
                return Results.Ok(ApiResponse<AdminDiscoveryOverviewDto>.Ok(result));
            })
            .WithName("AdminDiscoveryOverview")
            .WithTags("Administration", "Discovery")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/discovery/rankings", async (
                string? type,
                string? period,
                int? limit,
                ITrendingService trending,
                CancellationToken cancellationToken) =>
            {
                var result = await trending.GetRankingAsync(
                    type ?? DiscoveryRankingTypes.Trending, period ?? "24h", null, 1, limit ?? 50, cancellationToken);
                return Results.Ok(ApiResponse<DiscoveryRankingResponse>.Ok(result));
            })
            .WithName("AdminDiscoveryRankings")
            .WithTags("Administration", "Discovery")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/discovery/config", async (
                IDiscoveryScoreService scores,
                CancellationToken cancellationToken) =>
            {
                var result = await scores.GetConfigAsync(cancellationToken);
                return Results.Ok(ApiResponse<AdminDiscoveryConfigDto>.Ok(result));
            })
            .WithName("AdminGetDiscoveryConfig")
            .WithTags("Administration", "Discovery")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapPut("/api/v1/admin/discovery/config", async (
                UpsertDiscoveryConfigRequest request,
                IDiscoveryScoreService scores,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var adminId = RequireUser(currentUser);
                var result = await scores.UpdateConfigAsync(adminId, request, cancellationToken);
                return Results.Ok(ApiResponse<AdminDiscoveryConfigDto>.Ok(result));
            })
            .WithName("AdminUpdateDiscoveryConfig")
            .WithTags("Administration", "Discovery")
            .RequireAuthorization(Policies.SuperAdminOnly)
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/admin/discovery/recalculate", async (
                IMetricAggregationService aggregation,
                IDiscoveryScoreService scores,
                ITrendingService trending,
                CancellationToken cancellationToken) =>
            {
                await aggregation.AggregateAsync(cancellationToken);
                await scores.RecalculateAllAsync(cancellationToken);
                await trending.CalculateSnapshotsAsync(cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { recalculated = true }));
            })
            .WithName("AdminRecalculateDiscovery")
            .WithTags("Administration", "Discovery")
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

using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Recommendation.Data;
using GameDiscoveries.Modules.Recommendation.Domain;
using GameDiscoveries.Modules.Recommendation.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Recommendation.Features;

public static class RecommendationsEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/recommendations", async (
                string? type,
                int? limit,
                IRecommendationEngine engine,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var parsed = ParseType(type) ?? RecommendationType.ForYou;
                var userId = TryGetUserId(currentUser);
                var result = await engine.GetAsync(parsed, userId, null, limit, cancellationToken: cancellationToken);
                return Results.Ok(ApiResponse<RecommendationResponse>.Ok(result));
            })
            .WithName("GetRecommendations")
            .WithTags("Recommendations")
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .Produces<ApiResponse<RecommendationResponse>>(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/recommendations/home", async (
                int? limit,
                IRecommendationEngine engine,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = TryGetUserId(currentUser);
                var result = await engine.GetHomeAsync(userId, limit ?? 12, cancellationToken);
                return Results.Ok(ApiResponse<RecommendationHomeResponse>.Ok(result));
            })
            .WithName("GetRecommendationHome")
            .WithTags("Recommendations")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        MapTyped(endpoints, "/api/v1/recommendations/for-you", RecommendationType.ForYou, "GetForYouRecommendations");
        MapTyped(endpoints, "/api/v1/recommendations/because-you-played", RecommendationType.BecauseYouPlayed, "GetBecauseYouPlayedRecommendations");
        MapTyped(endpoints, "/api/v1/recommendations/trending", RecommendationType.Trending, "GetTrendingRecommendations");
        MapTyped(endpoints, "/api/v1/recommendations/new", RecommendationType.NewDiscoveries, "GetNewDiscoveryRecommendations");
        MapTyped(endpoints, "/api/v1/recommendations/hidden-gems", RecommendationType.HiddenGems, "GetHiddenGemsRecommendations");
        MapTyped(endpoints, "/api/v1/recommendations/quick-play", RecommendationType.QuickPlay, "GetQuickPlayRecommendations");

        endpoints.MapGet("/api/v1/recommendations/similar/{gameId:guid}", async (
                Guid gameId,
                int? limit,
                IRecommendationEngine engine,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = TryGetUserId(currentUser);
                var result = await engine.GetAsync(
                    RecommendationType.SimilarGames,
                    userId,
                    gameId,
                    limit,
                    cancellationToken: cancellationToken);
                return Results.Ok(ApiResponse<RecommendationResponse>.Ok(result));
            })
            .WithName("GetSimilarGameRecommendations")
            .WithTags("Recommendations")
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .Produces<ApiResponse<RecommendationResponse>>(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/recommendations/{gameId:guid}/feedback", async (
                Guid gameId,
                RecommendationFeedbackRequest request,
                IRecommendationTrackingStore tracking,
                IRecommendationCache cache,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var type = request.FeedbackType?.Trim().ToUpperInvariant();
                if (type is not ("LIKE" or "DISLIKE" or "NOT_INTERESTED" or "ALREADY_PLAYED" or "MORE_LIKE_THIS"))
                {
                    throw new ValidationException("Invalid feedback type.");
                }

                var userId = TryGetUserId(currentUser);
                await tracking.RecordFeedbackAsync(gameId, request with { FeedbackType = type }, userId, cancellationToken);
                if (userId is Guid uid)
                {
                    await cache.InvalidateUserAsync(uid, cancellationToken);
                }

                return Results.Ok(ApiResponse<object>.Ok(new { recorded = true }));
            })
            .WithName("PostRecommendationFeedback")
            .WithTags("Recommendations")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapPost("/api/v1/recommendations/impressions", async (
                RecommendationImpressionRequest request,
                IRecommendationTrackingStore tracking,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = TryGetUserId(currentUser);
                await tracking.RecordImpressionAsync(request, userId, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { recorded = true }));
            })
            .WithName("PostRecommendationImpression")
            .WithTags("Recommendations")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/recommendations/overview", async (
                IRecommendationTrackingStore tracking,
                CancellationToken cancellationToken) =>
            {
                var result = await tracking.GetOverviewAsync(cancellationToken);
                return Results.Ok(ApiResponse<AdminRecommendationOverviewDto>.Ok(result));
            })
            .WithName("AdminRecommendationOverview")
            .WithTags("Administration", "Recommendations")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public");

        endpoints.MapGet("/api/v1/admin/recommendations/debug", async (
                string? type,
                Guid? userId,
                Guid? gameId,
                int? limit,
                IRecommendationEngine engine,
                CancellationToken cancellationToken) =>
            {
                var parsed = ParseType(type) ?? RecommendationType.ForYou;
                var result = await engine.GetDebugAsync(parsed, userId, gameId, limit, cancellationToken);
                return Results.Ok(ApiResponse<RecommendationDebugResponse>.Ok(result));
            })
            .WithName("DebugRecommendations")
            .WithTags("Administration")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces<ApiResponse<RecommendationDebugResponse>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static void MapTyped(
        IEndpointRouteBuilder endpoints,
        string path,
        RecommendationType type,
        string name)
    {
        endpoints.MapGet(path, async (
                int? limit,
                IRecommendationEngine engine,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var userId = TryGetUserId(currentUser);
                var result = await engine.GetAsync(type, userId, null, limit, cancellationToken: cancellationToken);
                return Results.Ok(ApiResponse<RecommendationResponse>.Ok(result));
            })
            .WithName(name)
            .WithTags("Recommendations")
            .AllowAnonymous()
            .Produces<ApiResponse<RecommendationResponse>>(StatusCodes.Status200OK);
    }

    private static Guid? TryGetUserId(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return null;
        }

        return Guid.TryParse(currentUser.UserId, out var userId) ? userId : null;
    }

    private static RecommendationType? ParseType(string? type)
        => type?.Trim().ToLowerInvariant() switch
        {
            "for-you" or "foryou" => RecommendationType.ForYou,
            "similar" or "similar-games" => RecommendationType.SimilarGames,
            "because-you-played" or "because" => RecommendationType.BecauseYouPlayed,
            "trending" => RecommendationType.Trending,
            "new" or "new-discoveries" => RecommendationType.NewDiscoveries,
            "hidden-gems" or "hidden" => RecommendationType.HiddenGems,
            "quick-play" or "quickplay" => RecommendationType.QuickPlay,
            null or "" => null,
            _ => null
        };
}

using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Configuration;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Recommendation.Domain;
using GameDiscoveries.Modules.Recommendation.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

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
            .Produces<ApiResponse<RecommendationResponse>>(StatusCodes.Status200OK);

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
            .Produces<ApiResponse<RecommendationResponse>>(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/admin/recommendations/debug", async (
                string? type,
                Guid? userId,
                Guid? gameId,
                int? limit,
                IRecommendationEngine engine,
                IOptions<GameFeedSyncOptions> syncOptions,
                HttpRequest httpRequest,
                CancellationToken cancellationToken) =>
            {
                if (!IsAdminAuthorized(httpRequest, syncOptions.Value))
                {
                    return Results.Unauthorized();
                }

                var parsed = ParseType(type) ?? RecommendationType.ForYou;
                var result = await engine.GetDebugAsync(parsed, userId, gameId, limit, cancellationToken);
                return Results.Ok(ApiResponse<RecommendationDebugResponse>.Ok(result));
            })
            .WithName("DebugRecommendations")
            .WithTags("Administration")
            .Produces<ApiResponse<RecommendationDebugResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

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

    private static bool IsAdminAuthorized(HttpRequest request, GameFeedSyncOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AdminApiKey))
        {
            return false;
        }

        return request.Headers.TryGetValue("X-GameDiscoveries-Admin-Key", out var provided)
               && string.Equals(provided.ToString(), options.AdminApiKey, StringComparison.Ordinal);
    }
}

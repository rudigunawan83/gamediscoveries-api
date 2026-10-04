using GameDiscoveries.BuildingBlocks.Configuration;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.BuildingBlocks.Feeds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Administration.Features.SyncGameFeeds;

public static class SyncGameFeedsEndpoint
{
    public const string AdminKeyHeader = "X-GameDiscoveries-Admin-Key";

    public static IEndpointRouteBuilder MapSyncGameFeeds(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/admin/game-feeds/sync", async (
                SyncGameFeedRequest request,
                IGameFeedImportService importService,
                IOptions<GameFeedSyncOptions> syncOptions,
                HttpRequest httpRequest,
                CancellationToken cancellationToken) =>
            {
                if (!IsAuthorized(httpRequest, syncOptions.Value))
                {
                    return Results.Unauthorized();
                }

                var result = await importService.ImportAsync(request.FeedType, cancellationToken);
                return Results.Ok(ApiResponse<SyncGameFeedResponse>.Ok(SyncGameFeedResponseMapper.From(result)));
            })
            .WithName("SyncGameFeed")
            .WithTags("Administration")
            .Produces<ApiResponse<SyncGameFeedResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/api/v1/admin/game-feeds/sync-all", async (
                IGameFeedImportService importService,
                IOptions<GameFeedSyncOptions> syncOptions,
                HttpRequest httpRequest,
                CancellationToken cancellationToken) =>
            {
                if (!IsAuthorized(httpRequest, syncOptions.Value))
                {
                    return Results.Unauthorized();
                }

                var results = await importService.ImportAllAsync(cancellationToken);
                var payload = results.Select(SyncGameFeedResponseMapper.From).ToList();
                return Results.Ok(ApiResponse<IReadOnlyList<SyncGameFeedResponse>>.Ok(payload));
            })
            .WithName("SyncAllGameFeeds")
            .WithTags("Administration")
            .Produces<ApiResponse<IReadOnlyList<SyncGameFeedResponse>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static bool IsAuthorized(HttpRequest request, GameFeedSyncOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AdminApiKey))
        {
            return false;
        }

        return request.Headers.TryGetValue(AdminKeyHeader, out var provided)
               && string.Equals(provided.ToString(), options.AdminApiKey, StringComparison.Ordinal);
    }
}

using GameDiscoveries.BuildingBlocks.Configuration;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.BuildingBlocks.Feeds;
using GameDiscoveries.Modules.Administration.Features.SyncGameFeeds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Administration.Features.Providers;

public static class ProviderAdminEndpoints
{
    public static IEndpointRouteBuilder MapProviderAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/providers", (
                IOptions<GameFeedSyncOptions> syncOptions,
                HttpRequest httpRequest) =>
            {
                if (!SyncGameFeedsEndpoint.IsAuthorized(httpRequest, syncOptions.Value))
                {
                    return Results.Unauthorized();
                }

                var payload = new[]
                {
                    new
                    {
                        name = "GameMonetize",
                        type = "GameMonetize",
                        syncEnabled = syncOptions.Value.Enabled
                    }
                };

                return Results.Ok(ApiResponse<object>.Ok(payload));
            })
            .WithName("ListProviders")
            .WithTags("Administration")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapGet("/api/v1/admin/providers/gamemonetize/status", (
                IOptions<GameFeedSyncOptions> syncOptions,
                HttpRequest httpRequest) =>
            {
                if (!SyncGameFeedsEndpoint.IsAuthorized(httpRequest, syncOptions.Value))
                {
                    return Results.Unauthorized();
                }

                var payload = new
                {
                    provider = "GameMonetize",
                    syncEnabled = syncOptions.Value.Enabled,
                    supportedFeeds = Enum.GetNames<GameFeedType>(),
                    syncEndpoints = new[]
                    {
                        "POST /api/v1/admin/providers/gamemonetize/sync",
                        "POST /api/v1/admin/game-feeds/sync",
                        "POST /api/v1/admin/game-feeds/sync-all"
                    }
                };

                return Results.Ok(ApiResponse<object>.Ok(payload));
            })
            .WithName("GetGameMonetizeStatus")
            .WithTags("Administration")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/api/v1/admin/providers/gamemonetize/sync", async (
                SyncGameFeedRequest? request,
                IGameFeedImportService importService,
                IOptions<GameFeedSyncOptions> syncOptions,
                HttpRequest httpRequest,
                CancellationToken cancellationToken) =>
            {
                if (!SyncGameFeedsEndpoint.IsAuthorized(httpRequest, syncOptions.Value))
                {
                    return Results.Unauthorized();
                }

                var feedType = request?.FeedType ?? GameFeedType.Latest;
                var result = await importService.ImportAsync(feedType, cancellationToken);
                return Results.Ok(ApiResponse<SyncGameFeedResponse>.Ok(SyncGameFeedResponseMapper.From(result)));
            })
            .WithName("SyncGameMonetizeProvider")
            .WithTags("Administration")
            .Produces<ApiResponse<SyncGameFeedResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}

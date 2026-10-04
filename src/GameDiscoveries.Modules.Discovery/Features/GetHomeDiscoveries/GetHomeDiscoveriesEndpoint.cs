using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Discovery.Features.GetHomeDiscoveries;

public static class GetHomeDiscoveriesEndpoint
{
    public static RouteHandlerBuilder MapGetHomeDiscoveries(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/api/v1/discoveries/home", async (
                GetHomeDiscoveriesHandler handler,
                CancellationToken cancellationToken) =>
            {
                var data = await handler.HandleAsync(cancellationToken);
                return Results.Ok(ApiResponse<HomeDiscoveriesResponse>.Ok(data));
            })
            .WithName("GetHomeDiscoveries")
            .WithTags("Discovery")
            .Produces<ApiResponse<HomeDiscoveriesResponse>>(StatusCodes.Status200OK)
            .AllowAnonymous();
    }
}

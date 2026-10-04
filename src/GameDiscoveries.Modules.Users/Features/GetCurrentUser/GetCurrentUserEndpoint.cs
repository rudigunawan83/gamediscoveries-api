using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Users.Features.GetCurrentUser;

public static class GetCurrentUserEndpoint
{
    public static RouteHandlerBuilder MapGetCurrentUser(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/api/v1/users/me", async (
                GetCurrentUserHandler handler,
                CancellationToken cancellationToken) =>
            {
                var user = await handler.HandleAsync(cancellationToken);
                return Results.Ok(ApiResponse<UserResponse>.Ok(user));
            })
            .WithName("GetCurrentUser")
            .WithTags("Users")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}

using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Users.Features.Logout;

public static class LogoutEndpoint
{
    public static RouteHandlerBuilder MapLogout(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/api/v1/auth/logout", (
                ILoggerFactory loggerFactory) =>
            {
                // Local JWTs are currently stateless. Logout clears client state;
                // this endpoint acknowledges the operation for the FE contract.
                loggerFactory.CreateLogger("Logout").LogInformation("User logged out");
                return Results.Ok(ApiResponse<object>.Ok(new { loggedOut = true }));
            })
            .WithName("Logout")
            .WithTags("Authentication")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}

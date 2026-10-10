using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Users.Features.Preferences;

public static class PreferencesEndpoints
{
    public static RouteHandlerBuilder MapPreferences(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/api/v1/users/me/preferences", async (
                UpdatePreferencesRequest request,
                PreferencesHandler handler,
                CancellationToken cancellationToken) =>
            {
                var user = await handler.UpdateAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<UserResponse>.Ok(user));
            })
            .WithName("UpdateUserPreferences")
            .WithTags("Users")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}

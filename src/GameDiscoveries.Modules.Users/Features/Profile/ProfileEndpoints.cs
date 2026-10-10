using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Users.Features.Profile;

public static class ProfileEndpoints
{
    public static RouteHandlerBuilder MapProfile(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/api/v1/users/me/profile", async (
                UpdateProfileRequest request,
                ProfileHandler handler,
                CancellationToken cancellationToken) =>
            {
                var user = await handler.UpdateAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<UserResponse>.Ok(user));
            })
            .WithName("UpdateUserProfile")
            .WithTags("Users")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

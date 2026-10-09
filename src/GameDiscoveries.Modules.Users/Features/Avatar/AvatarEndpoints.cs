using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Users.Features.Avatar;

public static class AvatarEndpoints
{
    /// <summary>Hard cap on the multipart body; the handler enforces the configured file limit.</summary>
    private const long MaxMultipartBytes = 6 * 1024 * 1024;

    public static IEndpointRouteBuilder MapAvatar(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/users/me/avatar", async (
                IFormFile? file,
                HttpRequest request,
                AvatarHandler handler,
                CancellationToken cancellationToken) =>
            {
                var user = await handler.UploadAsync(file, $"{request.Scheme}://{request.Host}", cancellationToken);
                return Results.Ok(ApiResponse<UserResponse>.Ok(user));
            })
            .WithName("UploadAvatar")
            .WithTags("Users")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .DisableAntiforgery()
            .WithFormOptions(multipartBodyLengthLimit: MaxMultipartBytes)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapDelete("/api/v1/users/me/avatar", async (
                AvatarHandler handler,
                CancellationToken cancellationToken) =>
            {
                var user = await handler.RemoveAsync(cancellationToken);
                return Results.Ok(ApiResponse<UserResponse>.Ok(user));
            })
            .WithName("RemoveAvatar")
            .WithTags("Users")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapGet($"{AvatarStorage.RoutePrefix}/{{userId:guid}}/{{fileName}}", (
                Guid userId,
                string fileName,
                AvatarStorage storage,
                HttpResponse response) =>
            {
                var path = storage.TryGetFilePath(userId, fileName);
                if (path is null)
                {
                    return Results.NotFound();
                }

                response.Headers.CacheControl = "public, max-age=31536000, immutable";
                response.Headers.XContentTypeOptions = "nosniff";
                return Results.File(path, "image/jpeg");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }
}

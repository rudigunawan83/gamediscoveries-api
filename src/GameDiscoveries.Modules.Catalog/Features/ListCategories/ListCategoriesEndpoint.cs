using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Catalog.Features.ListCategories;

public static class ListCategoriesEndpoint
{
    public static RouteHandlerBuilder MapListCategories(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/api/v1/categories", async (
                ListCategoriesHandler handler,
                CancellationToken cancellationToken) =>
            {
                var items = await handler.HandleAsync(cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<CategoryResponse>>.Ok(items));
            })
            .WithName("ListCategories")
            .WithTags("Catalog")
            .Produces<ApiResponse<IReadOnlyList<CategoryResponse>>>(StatusCodes.Status200OK)
            .AllowAnonymous();
    }
}

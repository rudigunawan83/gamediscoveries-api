using FluentValidation;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Catalog.Features.ListGames;

public static class ListGamesEndpoint
{
    public static RouteHandlerBuilder MapListGames(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/api/v1/games", async (
                int? page,
                int? pageSize,
                string? search,
                string? category,
                string? platform,
                bool? mobileReady,
                string? sort,
                string? tag,
                ListGamesHandler handler,
                IValidator<ListGamesQuery> validator,
                CancellationToken cancellationToken) =>
            {
                var query = new ListGamesQuery
                {
                    Page = page ?? 1,
                    PageSize = pageSize ?? 20,
                    Search = search,
                    Category = category,
                    Platform = platform,
                    MobileReady = mobileReady,
                    Sort = sort,
                    Tag = tag
                };

                var validation = await validator.ValidateAsync(query, cancellationToken);
                if (!validation.IsValid)
                {
                    var errors = validation.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                    throw new BuildingBlocks.Errors.ValidationException(
                        "One or more validation errors occurred.",
                        errors);
                }

                var (items, meta) = await handler.HandleAsync(query, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<GameSummaryResponse>>.Ok(items, meta));
            })
            .WithName("ListGames")
            .WithTags("Catalog")
            .Produces<ApiResponse<IReadOnlyList<GameSummaryResponse>>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AllowAnonymous();
    }
}

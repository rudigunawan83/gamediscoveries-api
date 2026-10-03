using FluentValidation;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

public static class GetGameBySlugEndpoint
{
    public static RouteHandlerBuilder MapGetGameBySlug(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/api/v1/games/{slug}", async (
                string slug,
                GetGameBySlugHandler handler,
                IValidator<GetGameBySlugQuery> validator,
                CancellationToken cancellationToken) =>
            {
                var query = new GetGameBySlugQuery(slug);
                var validation = await validator.ValidateAsync(query, cancellationToken);

                if (!validation.IsValid)
                {
                    var errors = validation.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray());

                    throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException(
                        "One or more validation errors occurred.",
                        errors);
                }

                var game = await handler.HandleAsync(query, cancellationToken);
                return Results.Ok(ApiResponse<GameResponse>.Ok(game));
            })
            .WithName("GetGameBySlug")
            .WithTags("Catalog")
            .Produces<ApiResponse<GameResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AllowAnonymous();
    }
}

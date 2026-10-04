using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Favorites.Features.AddFavorite;
using GameDiscoveries.Modules.Favorites.Features.CheckFavorite;
using GameDiscoveries.Modules.Favorites.Features.ListFavorites;
using GameDiscoveries.Modules.Favorites.Features.ListHistory;
using GameDiscoveries.Modules.Favorites.Features.RecordHistory;
using GameDiscoveries.Modules.Favorites.Features.RemoveFavorite;
using GameDiscoveries.Modules.Favorites.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Favorites.Features;

public static class FavoritesEndpoints
{
    public static IEndpointRouteBuilder MapFavoritesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/users/me/favorites", async (
                int? page,
                int? pageSize,
                ListFavoritesHandler handler,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await handler.HandleAsync(page ?? 1, pageSize ?? 24, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<FavoriteItemResponse>>.Ok(items, meta));
            })
            .WithName("ListMyFavorites")
            .WithTags("Favorites")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<IReadOnlyList<FavoriteItemResponse>>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/api/v1/users/me/favorites", async (
                AddFavoriteRequest request,
                AddFavoriteHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { favorited = true }));
            })
            .WithName("AddMyFavorite")
            .WithTags("Favorites")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapGet("/api/v1/users/me/favorites/{gameId:guid}", async (
                Guid gameId,
                CheckFavoriteHandler handler,
                CancellationToken cancellationToken) =>
            {
                var favorited = await handler.HandleAsync(gameId, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { favorited }));
            })
            .WithName("CheckMyFavorite")
            .WithTags("Favorites")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapDelete("/api/v1/users/me/favorites/{gameId:guid}", async (
                Guid gameId,
                RemoveFavoriteHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(gameId, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { removed = true }));
            })
            .WithName("RemoveMyFavorite")
            .WithTags("Favorites")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/v1/users/me/history", async (
                int? page,
                int? pageSize,
                ListHistoryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await handler.HandleAsync(page ?? 1, pageSize ?? 24, cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<HistoryItemResponse>>.Ok(items, meta));
            })
            .WithName("ListMyHistory")
            .WithTags("History")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<IReadOnlyList<HistoryItemResponse>>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/api/v1/users/me/history", async (
                RecordHistoryRequest request,
                RecordHistoryHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<object>.Ok(new { recorded = true }));
            })
            .WithName("RecordMyHistory")
            .WithTags("History")
            .RequireAuthorization(Policies.Authenticated)
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

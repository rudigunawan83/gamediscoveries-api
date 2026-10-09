using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Favorites.Data;
using GameDiscoveries.Modules.Favorites.Features;
using GameDiscoveries.Modules.Favorites.Features.AddFavorite;
using GameDiscoveries.Modules.Favorites.Features.CheckFavorite;
using GameDiscoveries.Modules.Favorites.Features.ListFavorites;
using GameDiscoveries.Modules.Favorites.Features.ListHistory;
using GameDiscoveries.Modules.Favorites.Features.RecordHistory;
using GameDiscoveries.Modules.Favorites.Features.RemoveFavorite;
using GameDiscoveries.Modules.Favorites.Processing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Favorites;

public sealed class FavoritesModule : IModule
{
    public string Name => "Favorites";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IUserLibraryRepository, UserLibraryRepository>();
        services.AddScoped<ListFavoritesHandler>();
        services.AddScoped<AddFavoriteHandler>();
        services.AddScoped<CheckFavoriteHandler>();
        services.AddScoped<RemoveFavoriteHandler>();
        services.AddScoped<ListHistoryHandler>();
        services.AddScoped<RecordHistoryHandler>();
        services.AddScoped<IAnalyticsEventHandler, GameSessionHistoryEventHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFavoritesEndpoints();
    }
}

public static class FavoritesModuleExtensions
{
    public static IServiceCollection AddFavoritesModule(this IServiceCollection services)
    {
        var module = new FavoritesModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapFavoritesModule(this IEndpointRouteBuilder endpoints)
    {
        new FavoritesModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

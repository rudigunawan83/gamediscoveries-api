using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Favorites;

public sealed class FavoritesModule : IModule
{
    public string Name => "Favorites";

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
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
}

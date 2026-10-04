using FluentValidation;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;
using GameDiscoveries.Modules.Catalog.Features.ListCategories;
using GameDiscoveries.Modules.Catalog.Features.ListGames;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Catalog;

public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<GetGameBySlugHandler>();
        services.AddScoped<ListGamesHandler>();
        services.AddScoped<ListCategoriesHandler>();
        services.AddValidatorsFromAssemblyContaining<GetGameBySlugValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapListGames();
        endpoints.MapGetGameBySlug();
        endpoints.MapListCategories();
    }
}

public static class CatalogModuleExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        var module = new CatalogModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogModule(this IEndpointRouteBuilder endpoints)
    {
        new CatalogModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

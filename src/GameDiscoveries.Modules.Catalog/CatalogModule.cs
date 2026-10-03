using FluentValidation;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Catalog;

public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<GetGameBySlugHandler>();
        services.AddValidatorsFromAssemblyContaining<GetGameBySlugValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGetGameBySlug();
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

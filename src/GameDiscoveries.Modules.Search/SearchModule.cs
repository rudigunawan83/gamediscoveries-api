using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Search;

public sealed class SearchModule : IModule
{
    public string Name => "Search";

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

public static class SearchModuleExtensions
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services)
    {
        var module = new SearchModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }
}

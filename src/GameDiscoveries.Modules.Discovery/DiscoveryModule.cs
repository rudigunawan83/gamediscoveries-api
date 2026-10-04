using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Discovery.Features.GetHomeDiscoveries;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Discovery;

public sealed class DiscoveryModule : IModule
{
    public string Name => "Discovery";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<GetHomeDiscoveriesHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGetHomeDiscoveries();
    }
}

public static class DiscoveryModuleExtensions
{
    public static IServiceCollection AddDiscoveryModule(this IServiceCollection services)
    {
        var module = new DiscoveryModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapDiscoveryModule(this IEndpointRouteBuilder endpoints)
    {
        new DiscoveryModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

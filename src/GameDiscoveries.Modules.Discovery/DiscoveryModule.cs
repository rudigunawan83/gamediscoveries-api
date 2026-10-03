using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Discovery;

public sealed class DiscoveryModule : IModule
{
    public string Name => "Discovery";

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
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
}

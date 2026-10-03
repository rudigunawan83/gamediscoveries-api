using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Providers;

public sealed class ProvidersModule : IModule
{
    public string Name => "Providers";

    public void RegisterServices(IServiceCollection services)
    {
        // Provider sync features will be added in a later phase.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // No public endpoints in foundation phase.
    }
}

public static class ProvidersModuleExtensions
{
    public static IServiceCollection AddProvidersModule(this IServiceCollection services)
    {
        var module = new ProvidersModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }
}

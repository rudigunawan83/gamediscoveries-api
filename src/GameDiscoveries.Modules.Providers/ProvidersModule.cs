using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Providers;

public sealed class ProvidersModule : IModule
{
    public string Name => "Providers";

    public void RegisterServices(IServiceCollection services)
    {
        // Provider feed import/sync is hosted in Infrastructure (GameFeedImportService).
        // Public provider endpoints remain intentionally empty.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Sync is exposed via Administration module admin endpoints.
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

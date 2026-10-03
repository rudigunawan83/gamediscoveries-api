using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Advertising;

public sealed class AdvertisingModule : IModule
{
    public string Name => "Advertising";

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

public static class AdvertisingModuleExtensions
{
    public static IServiceCollection AddAdvertisingModule(this IServiceCollection services)
    {
        var module = new AdvertisingModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }
}

using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Administration;

public sealed class AdministrationModule : IModule
{
    public string Name => "Administration";

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

public static class AdministrationModuleExtensions
{
    public static IServiceCollection AddAdministrationModule(this IServiceCollection services)
    {
        var module = new AdministrationModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }
}

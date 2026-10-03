using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Developer;

public sealed class DeveloperModule : IModule
{
    public string Name => "Developer";

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

public static class DeveloperModuleExtensions
{
    public static IServiceCollection AddDeveloperModule(this IServiceCollection services)
    {
        var module = new DeveloperModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }
}

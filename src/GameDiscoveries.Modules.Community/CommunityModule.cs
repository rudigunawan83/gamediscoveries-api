using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Community.Domain;
using GameDiscoveries.Modules.Community.Features;
using GameDiscoveries.Modules.Community.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Community;

public sealed class CommunityModule : IModule
{
    public string Name => "Community";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<CommunityService>();
        services.AddScoped<ICommunityService>(sp => sp.GetRequiredService<CommunityService>());
        services.AddScoped<ICommunityGameSignalsService>(sp => sp.GetRequiredService<CommunityService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCommunityEndpoints();
    }
}

public static class CommunityModuleExtensions
{
    public static IServiceCollection AddCommunityModule(this IServiceCollection services)
    {
        var module = new CommunityModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapCommunityModule(this IEndpointRouteBuilder endpoints)
    {
        new CommunityModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

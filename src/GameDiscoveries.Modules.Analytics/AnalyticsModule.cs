using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Data;
using GameDiscoveries.Modules.Analytics.Features.TrackEvent;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Analytics;

public sealed class AnalyticsModule : IModule
{
    public string Name => "Analytics";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IAnalyticsEventStore, AnalyticsEventStore>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTrackEvent();
    }
}

public static class AnalyticsModuleExtensions
{
    public static IServiceCollection AddAnalyticsModule(this IServiceCollection services)
    {
        var module = new AnalyticsModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapAnalyticsModule(this IEndpointRouteBuilder endpoints)
    {
        new AnalyticsModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

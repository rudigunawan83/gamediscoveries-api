using GameDiscoveries.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Recommendation;

public sealed class RecommendationModule : IModule
{
    public string Name => "Recommendation";

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

public static class RecommendationModuleExtensions
{
    public static IServiceCollection AddRecommendationModule(this IServiceCollection services)
    {
        var module = new RecommendationModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }
}

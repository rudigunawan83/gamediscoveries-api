using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Recommendation.Data;
using GameDiscoveries.Modules.Recommendation.Features;
using GameDiscoveries.Modules.Recommendation.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Recommendation;

public sealed class RecommendationModule : IModule
{
    public string Name => "Recommendation";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IRecommendationCache, RecommendationCache>();
        services.AddScoped<IRecommendationEngine, RecommendationEngine>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRecommendationsEndpoints();
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

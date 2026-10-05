using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.DiscoveryScore.Data;
using GameDiscoveries.Modules.DiscoveryScore.Domain;
using GameDiscoveries.Modules.DiscoveryScore.Features;
using GameDiscoveries.Modules.DiscoveryScore.Options;
using GameDiscoveries.Modules.DiscoveryScore.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.DiscoveryScore;

public sealed class DiscoveryScoreModule : IModule
{
    public string Name => "DiscoveryScore";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<DiscoveryScoreOptions>()
            .BindConfiguration(DiscoveryScoreOptions.SectionName)
            .Validate(o => DiscoveryScoreFormulas.ValidateWeights(
                    o.PopularityWeight, o.EngagementWeight, o.QualityWeight,
                    o.MomentumWeight, o.GrowthWeight, o.FreshnessWeight),
                "DiscoveryScore component weights must sum to 1.0.")
            .Validate(o => DiscoveryScoreFormulas.ValidateWeights(
                    o.TrendingRecentWeight, o.TrendingMomentumWeight, o.TrendingGrowthWeight,
                    o.TrendingEngagementWeight, o.TrendingFreshnessWeight),
                "DiscoveryScore trending weights must sum to 1.0.")
            .Validate(o => o.AggregationIntervalMinutes is >= 5 and <= 360,
                "DiscoveryScore:AggregationIntervalMinutes must be 5..360.")
            .ValidateOnStart();

        services.AddScoped<IDiscoveryScoreStore, DiscoveryScoreStore>();
        services.AddScoped<IMetricAggregationService, MetricAggregationService>();
        services.AddScoped<IDiscoveryScoreService, DiscoveryScoreService>();
        services.AddScoped<TrendingService>();
        services.AddScoped<ITrendingService>(sp => sp.GetRequiredService<TrendingService>());
        services.AddScoped<IDiscoveryRankingProvider>(sp => sp.GetRequiredService<TrendingService>());
        services.AddHostedService<DiscoveryScoreHostedService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDiscoveryScoreEndpoints();
    }
}

public static class DiscoveryScoreModuleExtensions
{
    public static IServiceCollection AddDiscoveryScoreModule(this IServiceCollection services)
    {
        var module = new DiscoveryScoreModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapDiscoveryScoreModule(this IEndpointRouteBuilder endpoints)
    {
        new DiscoveryScoreModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

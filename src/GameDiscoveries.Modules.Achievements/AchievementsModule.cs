using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Achievements.Data;
using GameDiscoveries.Modules.Achievements.Evaluation;
using GameDiscoveries.Modules.Achievements.Features;
using GameDiscoveries.Modules.Achievements.Options;
using GameDiscoveries.Modules.Achievements.Processing;
using GameDiscoveries.Modules.Achievements.Services;
using GameDiscoveries.Modules.Analytics.Processing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Achievements;

public sealed class AchievementsModule : IModule
{
    public string Name => "Achievements";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<AchievementOptions>()
            .BindConfiguration(AchievementOptions.SectionName)
            .ValidateOnStart();

        services.AddScoped<IAchievementStore, AchievementStore>();
        services.AddScoped<IRequirementEvaluator, CompositeRequirementEvaluator>();
        services.AddScoped<AchievementService>();
        services.AddScoped<IAchievementService>(sp => sp.GetRequiredService<AchievementService>());
        services.AddScoped<IAchievementActivitySink>(sp => sp.GetRequiredService<AchievementService>());
        services.AddScoped<IAnalyticsEventHandler, AchievementAnalyticsEventHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAchievementEndpoints();
    }
}

public static class AchievementsModuleExtensions
{
    public static IServiceCollection AddAchievementsModule(this IServiceCollection services)
    {
        var module = new AchievementsModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapAchievementsModule(this IEndpointRouteBuilder endpoints)
    {
        new AchievementsModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

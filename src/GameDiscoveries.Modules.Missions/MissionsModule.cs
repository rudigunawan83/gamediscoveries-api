using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Missions.Data;
using GameDiscoveries.Modules.Missions.Features;
using GameDiscoveries.Modules.Missions.Options;
using GameDiscoveries.Modules.Missions.Processing;
using GameDiscoveries.Modules.Missions.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Missions;

public sealed class MissionsModule : IModule
{
    public string Name => "Missions";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<MissionsOptions>()
            .BindConfiguration(MissionsOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.TimeZone), "Missions:TimeZone is required.")
            .Validate(o => o.DailyMissionCount is > 0 and <= 10, "Missions:DailyMissionCount must be 1..10.")
            .Validate(o => o.WeeklyChallengeCount is > 0 and <= 10, "Missions:WeeklyChallengeCount must be 1..10.")
            .ValidateOnStart();

        services.AddSingleton<IMissionPeriodService, MissionPeriodService>();
        services.AddSingleton<IMissionSelectionStrategy, DefaultMissionSelectionStrategy>();
        services.AddScoped<IMissionStore, MissionStore>();
        services.AddScoped<IMissionAssignmentService, MissionAssignmentService>();
        services.AddScoped<MissionService>();
        services.AddScoped<IMissionService>(sp => sp.GetRequiredService<MissionService>());
        services.AddScoped<IMissionActivitySink>(sp => sp.GetRequiredService<MissionService>());
        services.AddScoped<IAdminMissionService, AdminMissionService>();
        services.AddScoped<IAnalyticsEventHandler, GameSessionMissionEventHandler>();
        services.AddHostedService<MissionExpirationHostedService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMissionEndpoints();
    }
}

public static class MissionsModuleExtensions
{
    public static IServiceCollection AddMissionsModule(this IServiceCollection services)
    {
        var module = new MissionsModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapMissionsModule(this IEndpointRouteBuilder endpoints)
    {
        new MissionsModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

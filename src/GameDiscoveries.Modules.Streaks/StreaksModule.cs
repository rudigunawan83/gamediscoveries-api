using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Streaks.Data;
using GameDiscoveries.Modules.Streaks.Features;
using GameDiscoveries.Modules.Streaks.Options;
using GameDiscoveries.Modules.Streaks.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Streaks;

public sealed class StreaksModule : IModule
{
    public string Name => "Streaks";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<StreakOptions>()
            .BindConfiguration(StreakOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.TimeZone), "Streak:TimeZone is required.")
            .Validate(o => o.MinimumActiveSeconds is > 0 and <= 3600, "Streak:MinimumActiveSeconds must be 1..3600.")
            .Validate(o => o.Freeze.MaxStored is >= 0 and <= 10, "Streak:Freeze:MaxStored must be 0..10.")
            .ValidateOnStart();

        services.AddSingleton<IStreakTimeService, StreakTimeService>();
        services.AddScoped<IQualifyingActivityService, QualifyingActivityService>();
        services.AddScoped<IStreakStore, StreakStore>();
        services.AddScoped<StreakService>();
        services.AddScoped<IStreakService>(sp => sp.GetRequiredService<StreakService>());
        services.AddScoped<IMissionActivitySink>(sp => sp.GetRequiredService<StreakService>());
        services.AddScoped<IStreakProgressProvider>(sp => sp.GetRequiredService<StreakService>());
        services.AddSingleton<IStreakRecoveryService, NoOpStreakRecoveryService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapStreakEndpoints();
    }
}

public static class StreaksModuleExtensions
{
    public static IServiceCollection AddStreaksModule(this IServiceCollection services)
    {
        var module = new StreaksModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapStreaksModule(this IEndpointRouteBuilder endpoints)
    {
        new StreaksModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Leaderboards.Data;
using GameDiscoveries.Modules.Leaderboards.Features;
using GameDiscoveries.Modules.Leaderboards.Options;
using GameDiscoveries.Modules.Leaderboards.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Leaderboards;

public sealed class LeaderboardsModule : IModule
{
    public string Name => "Leaderboards";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<LeaderboardOptions>()
            .BindConfiguration(LeaderboardOptions.SectionName)
            .Validate(o => o.TopMaxLimit is >= 10 and <= 100, "Leaderboard:TopMaxLimit must be 10..100.")
            .Validate(o => o.ReconciliationIntervalMinutes is >= 15 and <= 360, "Leaderboard:ReconciliationIntervalMinutes must be 15..360.")
            .ValidateOnStart();

        services.AddScoped<ILeaderboardStore, LeaderboardStore>();
        services.AddScoped<ILeaderboardEligibilityService, LeaderboardEligibilityService>();
        services.AddSingleton<ILeaderboardScopeProvider, GlobalScopeProvider>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<ILeaderboardService>(sp => sp.GetRequiredService<LeaderboardService>());
        services.AddScoped<ILeaderboardXpSink>(sp => sp.GetRequiredService<LeaderboardService>());
        services.AddHostedService<LeaderboardHostedService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLeaderboardEndpoints();
    }
}

public static class LeaderboardsModuleExtensions
{
    public static IServiceCollection AddLeaderboardsModule(this IServiceCollection services)
    {
        var module = new LeaderboardsModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapLeaderboardsModule(this IEndpointRouteBuilder endpoints)
    {
        new LeaderboardsModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

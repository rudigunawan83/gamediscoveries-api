using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Data;
using GameDiscoveries.Modules.Analytics.Features.IngestEvents;
using GameDiscoveries.Modules.Analytics.Features.PlaySessions;
using GameDiscoveries.Modules.Analytics.Features.TrackEvent;
using GameDiscoveries.Modules.Analytics.Options;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Analytics.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Analytics;

public sealed class AnalyticsModule : IModule
{
    public string Name => "Analytics";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<AnalyticsOptions>()
            .BindConfiguration(AnalyticsOptions.SectionName)
            .Validate(o => o.MaxBatchSize is > 0 and <= 500, "Analytics:MaxBatchSize must be 1..500.")
            .Validate(o => o.MaxMetadataSizeKb is > 0 and <= 64, "Analytics:MaxMetadataSizeKb must be 1..64.")
            .ValidateOnStart();

        services.AddOptions<GameSessionOptions>()
            .BindConfiguration(GameSessionOptions.SectionName)
            .Validate(o => o.HeartbeatIntervalSeconds is >= 5 and <= 120, "GameSession:HeartbeatIntervalSeconds must be 5..120.")
            .Validate(o => o.HeartbeatTimeoutSeconds is >= 30 and <= 600, "GameSession:HeartbeatTimeoutSeconds must be 30..600.")
            .Validate(o => o.MinimumValidActiveSeconds is >= 0 and <= 600, "GameSession:MinimumValidActiveSeconds must be 0..600.")
            .Validate(o => o.MaximumSessionDurationSeconds is >= 60 and <= 86_400, "GameSession:MaximumSessionDurationSeconds must be 60..86400.")
            .ValidateOnStart();

        services.AddScoped<IAnalyticsEventStore, AnalyticsEventStore>();
        services.AddScoped<IAnalyticsEventValidator, AnalyticsEventValidator>();
        services.AddScoped<IAnalyticsEventService, AnalyticsEventService>();
        services.AddScoped<IAnalyticsEventDispatcher, AnalyticsEventDispatcher>();
        services.AddScoped<IGamePlaySessionStore, GamePlaySessionStore>();
        services.AddScoped<IGamePlaySessionService, GamePlaySessionService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapIngestEvents();
        endpoints.MapTrackEvent();
        endpoints.MapPlaySessions();
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

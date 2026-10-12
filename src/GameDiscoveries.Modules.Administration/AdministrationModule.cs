using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Administration.Features.Monitoring;
using GameDiscoveries.Modules.Administration.Features.Providers;
using GameDiscoveries.Modules.Administration.Features.SyncGameFeeds;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameDiscoveries.Modules.Administration;

public sealed class AdministrationModule : IModule
{
    public string Name => "Administration";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IMonitoringService, MonitoringService>();
        services.AddScoped<ITrafficMonitoringService, TrafficMonitoringService>();
        services.AddScoped<IGameAnalyticsService, GameAnalyticsService>();
        services.AddScoped<IUserRetentionService, UserRetentionService>();
        services.AddScoped<IGamificationAnalyticsService, GamificationAnalyticsService>();

        services.AddOptions<SearchConsoleOptions>()
            .BindConfiguration(SearchConsoleOptions.SectionName)
            .Validate(
                o => Uri.TryCreate(o.SiteUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "SearchConsole:SiteUrl must be an absolute https URL.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(SearchConsoleClient.HttpClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<ISearchConsoleClient, SearchConsoleClient>();
        services.AddScoped<ISeoMonitoringService, SeoMonitoringService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSyncGameFeeds();
        endpoints.MapProviderAdminEndpoints();
        endpoints.MapMonitoring();
    }
}

public static class AdministrationModuleExtensions
{
    public static IServiceCollection AddAdministrationModule(this IServiceCollection services)
    {
        var module = new AdministrationModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapAdministrationModule(this IEndpointRouteBuilder endpoints)
    {
        new AdministrationModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

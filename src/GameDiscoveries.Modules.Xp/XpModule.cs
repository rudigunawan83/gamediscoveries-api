using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Xp.Data;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Features;
using GameDiscoveries.Modules.Xp.Options;
using GameDiscoveries.Modules.Xp.Processing;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Xp;

public sealed class XpModule : IModule
{
    public string Name => "Xp";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<XpOptions>()
            .BindConfiguration(XpOptions.SectionName)
            .Validate(o => o.DailyXpCap is > 0 and <= 100_000, "Xp:DailyXpCap must be 1..100000.")
            .ValidateOnStart();

        services.AddSingleton<IXpRuleCatalog, XpRuleCatalog>();
        services.AddSingleton<ILevelService, LevelService>();
        services.AddScoped<IXpStore, XpStore>();
        services.AddScoped<IXpEngine, XpEngine>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAdminGamificationService, AdminGamificationService>();
        services.AddScoped<IAnalyticsEventHandler, GameSessionXpEventHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapXpEndpoints();
    }
}

public static class XpModuleExtensions
{
    public static IServiceCollection AddXpModule(this IServiceCollection services)
    {
        var module = new XpModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapXpModule(this IEndpointRouteBuilder endpoints)
    {
        new XpModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Advertising.Data;
using GameDiscoveries.Modules.Advertising.Features;
using GameDiscoveries.Modules.Advertising.Options;
using GameDiscoveries.Modules.Advertising.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameDiscoveries.Modules.Advertising;

public sealed class AdvertisingModule : IModule
{
    public string Name => "Advertising";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddOptions<AdRewardOptions>()
            .BindConfiguration(AdRewardOptions.SectionName)
            .Validate(o => o.DailyLimit is >= 1 and <= 50, "AdRewards:DailyLimit must be 1..50.")
            .Validate(o => o.TicketTtlMinutes is >= 5 and <= 24 * 60, "AdRewards:TicketTtlMinutes must be 5..1440.")
            .Validate(
                o => Uri.TryCreate(o.VerifierKeysUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "AdRewards:VerifierKeysUrl must be an absolute https URL.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(HttpAdMobVerifierKeyProvider.HttpClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IAdMobVerifierKeyProvider, HttpAdMobVerifierKeyProvider>();
        services.AddSingleton<IAdMobSsvVerifier, AdMobSsvVerifier>();
        services.AddScoped<IAdRewardStore, AdRewardStore>();
        services.AddScoped<IAdRewardService, AdRewardService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdRewardEndpoints();
    }
}

public static class AdvertisingModuleExtensions
{
    public static IServiceCollection AddAdvertisingModule(this IServiceCollection services)
    {
        var module = new AdvertisingModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapAdvertisingModule(this IEndpointRouteBuilder endpoints)
    {
        new AdvertisingModule().MapEndpoints(endpoints);
        return endpoints;
    }
}

using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.BuildingBlocks;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<UserContext>();
        services.AddSingleton<IIntegrationEventPublisher, NoOpIntegrationEventPublisher>();

        return services;
    }
}

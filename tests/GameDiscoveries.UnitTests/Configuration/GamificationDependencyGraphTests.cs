using System.Reflection;
using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Achievements;
using GameDiscoveries.Modules.Advertising;
using GameDiscoveries.Modules.Advertising.Services;
using GameDiscoveries.Modules.Achievements.Services;
using GameDiscoveries.Modules.Analytics;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Analytics.Services;
using GameDiscoveries.Modules.Leaderboards;
using GameDiscoveries.Modules.Missions;
using GameDiscoveries.Modules.Missions.Services;
using GameDiscoveries.Modules.Streaks;
using GameDiscoveries.Modules.Streaks.Services;
using GameDiscoveries.Modules.Xp;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.UnitTests.Configuration;

public sealed class GamificationDependencyGraphTests
{
    public static TheoryData<Type> GamificationServices => new()
    {
        typeof(IXpEngine),
        typeof(IProgressService),
        typeof(IMissionService),
        typeof(IStreakService),
        typeof(IAchievementService),
        typeof(IAnalyticsEventService),
        typeof(IGamePlaySessionService),
        typeof(IEnumerable<IAnalyticsEventHandler>),
        typeof(IEnumerable<IAchievementActivitySink>),
        typeof(IEnumerable<IMissionActivitySink>),
        typeof(IAdRewardService)
    };

    [Theory]
    [MemberData(nameof(GamificationServices))]
    public void Service_resolves_without_circular_dependency(Type serviceType)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService(serviceType);

        act.Should().NotThrow();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddSingleton(Unused<IDbConnectionFactory>());
        services.AddSingleton(Unused<ICacheService>());

        services.AddAnalyticsModule();
        services.AddXpModule();
        services.AddAchievementsModule();
        services.AddMissionsModule();
        services.AddStreaksModule();
        services.AddLeaderboardsModule();
        services.AddAdvertisingModule();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static T Unused<T>() where T : class => DispatchProxy.Create<T, UnusedProxy>();

    public class UnusedProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"{targetMethod?.Name} must not be called while resolving services.");
    }
}

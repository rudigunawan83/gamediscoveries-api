using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Infrastructure.Meilisearch;
using GameDiscoveries.Infrastructure.PostgreSQL;
using GameDiscoveries.Infrastructure.Providers.Abstractions;
using GameDiscoveries.Infrastructure.Providers.GameMonetize;
using GameDiscoveries.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GameDiscoveries.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Database:ConnectionString is required.")
            .ValidateOnStart();

        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<MeilisearchOptions>()
            .Bind(configuration.GetSection(MeilisearchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<GameMonetizeOptions>()
            .Bind(configuration.GetSection(GameMonetizeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<DatabaseMigrator>();

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
                           ?? new RedisOptions();

        if (redisOptions.Enabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, NoOpCacheService>();
        }

        var meiliOptions = configuration.GetSection(MeilisearchOptions.SectionName).Get<MeilisearchOptions>()
                           ?? new MeilisearchOptions();

        if (meiliOptions.Enabled)
        {
            services.AddSingleton<ISearchService, MeilisearchService>();
        }
        else
        {
            services.AddSingleton<ISearchService, NoOpSearchService>();
        }

        services.AddSingleton<MeilisearchHealthCheck>();

        services.AddHttpClient<GameMonetizeClient>();
        services.AddSingleton<IGameProvider, GameMonetizeProvider>();

        return services;
    }

    public static void ApplyDatabaseMigrations(this IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.ApplyMigrationsOnStartup)
        {
            return;
        }

        var migrator = services.GetRequiredService<DatabaseMigrator>();
        migrator.Migrate();
    }
}

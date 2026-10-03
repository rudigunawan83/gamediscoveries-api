using GameDiscoveries.Infrastructure.Meilisearch;
using GameDiscoveries.Infrastructure.PostgreSQL;
using GameDiscoveries.Infrastructure.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Api.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddGameDiscoveriesHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
        var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();

        var builder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        if (!string.IsNullOrWhiteSpace(database?.ConnectionString))
        {
            builder.AddNpgSql(
                database.ConnectionString,
                name: "postgresql",
                tags: ["ready"]);
        }

        if (redis?.Enabled == true && !string.IsNullOrWhiteSpace(redis.ConnectionString))
        {
            builder.AddRedis(
                redis.ConnectionString,
                name: "redis",
                tags: ["ready"]);
        }

        var meili = configuration.GetSection(MeilisearchOptions.SectionName).Get<MeilisearchOptions>();
        if (meili?.Enabled == true)
        {
            builder.AddCheck<MeilisearchHealthCheck>("meilisearch", tags: ["ready"]);
        }

        return services;
    }

    public static WebApplication MapGameDiscoveriesHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return app;
    }
}

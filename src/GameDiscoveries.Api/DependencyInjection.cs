using System.Threading.RateLimiting;
using GameDiscoveries.Api.Extensions;
using GameDiscoveries.Api.OpenApi;
using GameDiscoveries.Api.Options;
using GameDiscoveries.BuildingBlocks;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Observability;
using GameDiscoveries.Infrastructure;
using GameDiscoveries.Modules.Administration;
using GameDiscoveries.Modules.Advertising;
using GameDiscoveries.Modules.Analytics;
using GameDiscoveries.Modules.Catalog;
using GameDiscoveries.Modules.Developer;
using GameDiscoveries.Modules.Discovery;
using GameDiscoveries.Modules.Favorites;
using GameDiscoveries.Modules.Providers;
using GameDiscoveries.Modules.Recommendation;
using GameDiscoveries.Modules.Search;
using GameDiscoveries.Modules.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using CorsOptions = GameDiscoveries.Api.Options.CorsOptions;
using OpenTelemetryOptions = GameDiscoveries.Api.Options.OpenTelemetryOptions;

namespace GameDiscoveries.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddGameDiscoveriesApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));
        services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddBuildingBlocks();
        services.AddInfrastructure(configuration);
        services.AddGameDiscoveriesHealthChecks(configuration);
        services.AddGameDiscoveriesOpenApi();
        services.AddAuthenticationFoundation(configuration);
        services.AddCorsPolicy(configuration);
        services.AddRateLimiting(configuration);
        services.AddObservability(configuration);
        services.AddModules();

        return services;
    }

    private static IServiceCollection AddModules(this IServiceCollection services)
    {
        services.AddCatalogModule();
        services.AddProvidersModule();
        services.AddDiscoveryModule();
        services.AddRecommendationModule();
        services.AddSearchModule();
        services.AddUsersModule();
        services.AddFavoritesModule();
        services.AddAnalyticsModule();
        services.AddDeveloperModule();
        services.AddAdvertisingModule();
        services.AddAdministrationModule();
        return services;
    }

    private static IServiceCollection AddAuthenticationFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authOptions = configuration
            .GetSection(AuthenticationOptions.SectionName)
            .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        // JWT/OAuth/OIDC-ready scaffold. Enable via Authentication:Enabled + Authority.
        var authBuilder = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

        if (authOptions.Enabled && !string.IsNullOrWhiteSpace(authOptions.Authority))
        {
            authBuilder.AddJwtBearer(options =>
            {
                options.Authority = authOptions.Authority;
                options.Audience = authOptions.Audience;
                options.RequireHttpsMetadata = authOptions.RequireHttpsMetadata;
            });
        }
        else
        {
            authBuilder.AddJwtBearer();
        }

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = null;

            options.AddPolicy(Policies.Authenticated, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(Policies.AdminOnly, policy =>
                policy.RequireRole(Roles.Admin, Roles.SuperAdmin));

            options.AddPolicy(Policies.DeveloperOnly, policy =>
                policy.RequireRole(Roles.Developer, Roles.Admin, Roles.SuperAdmin));

            options.AddPolicy(Policies.ModeratorOrAdmin, policy =>
                policy.RequireRole(Roles.Moderator, Roles.Admin, Roles.SuperAdmin));
        });

        return services;
    }

    private static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
                   ?? new CorsOptions();

        services.AddCors(options =>
        {
            options.AddPolicy("Default", policy =>
            {
                if (cors.AllowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(cors.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });

        return services;
    }

    private static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimiting = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                           ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("public", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimiting.PublicPermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy("search", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimiting.SearchPermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy("authentication", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimiting.AuthenticationPermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy("analytics", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimiting.AnalyticsPermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimiting.PublicPermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otel = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
                   ?? new OpenTelemetryOptions();

        if (!otel.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(otel.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivitySources.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otel.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otel.OtlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (otel.PrometheusEnabled)
                {
                    metrics.AddPrometheusExporter();
                }

                if (!string.IsNullOrWhiteSpace(otel.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otel.OtlpEndpoint));
                }
            });

        return services;
    }
}

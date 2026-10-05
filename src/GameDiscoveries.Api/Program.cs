using GameDiscoveries.Api;
using GameDiscoveries.Api.Extensions;
using GameDiscoveries.Api.Middleware;
using GameDiscoveries.Api.OpenApi;
using GameDiscoveries.Api.Options;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Infrastructure;
using GameDiscoveries.Modules.Administration;
using GameDiscoveries.Modules.Achievements;
using GameDiscoveries.Modules.Analytics;
using GameDiscoveries.Modules.Catalog;
using GameDiscoveries.Modules.Community;
using GameDiscoveries.Modules.Discovery;
using GameDiscoveries.Modules.Favorites;
using GameDiscoveries.Modules.Recommendation;
using GameDiscoveries.Modules.Users;
using GameDiscoveries.Modules.Xp;
using GameDiscoveries.Modules.Missions;
using GameDiscoveries.Modules.Streaks;
using GameDiscoveries.Modules.DiscoveryScore;
using GameDiscoveries.Modules.Leaderboards;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using OpenTelemetryOptions = GameDiscoveries.Api.Options.OpenTelemetryOptions;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .Enrich.WithSpan()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithThreadId()
        .Enrich.WithSpan()
        .WriteTo.Console());

    builder.Services.AddGameDiscoveriesApi(builder.Configuration);

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.ApplyDatabaseMigrations();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();

    app.MapGameDiscoveriesOpenApi();
    app.UseCors("Default");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    var otel = app.Configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>();
    if (otel?.Enabled == true && otel.PrometheusEnabled)
    {
        app.MapPrometheusScrapingEndpoint("/metrics");
    }

    app.MapGameDiscoveriesHealthChecks();
    app.MapCatalogModule();
    app.MapDiscoveryModule();
    app.MapRecommendationModule();
    app.MapCommunityModule();
    app.MapAnalyticsModule();
    app.MapXpModule();
    app.MapMissionsModule();
    app.MapStreaksModule();
    app.MapAchievementsModule();
    app.MapDiscoveryScoreModule();
    app.MapLeaderboardsModule();
    app.MapAdministrationModule();
    app.MapUsersModule();
    app.MapFavoritesModule();

    foreach (var module in app.Services.GetServices<IModule>())
    {
        Log.Information("Registered module {ModuleName}", module.Name);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

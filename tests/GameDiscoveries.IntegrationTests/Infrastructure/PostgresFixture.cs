using GameDiscoveries.Infrastructure.PostgreSQL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace GameDiscoveries.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .WithDatabase("gamediscoveries")
        .WithUsername("gamediscoveries")
        .WithPassword("gamediscoveries")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var migrator = new DatabaseMigrator(
            Microsoft.Extensions.Options.Options.Create(new DatabaseOptions
            {
                ConnectionString = ConnectionString,
                ApplyMigrationsOnStartup = true
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseMigrator>.Instance);

        migrator.Migrate();
    }

    public WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:ConnectionString"] = ConnectionString,
                        ["Database:ApplyMigrationsOnStartup"] = "false",
                        ["Redis:Enabled"] = "false",
                        ["Redis:ConnectionString"] = "localhost:6379",
                        ["Meilisearch:Enabled"] = "false",
                        ["Meilisearch:Url"] = "http://localhost:7700",
                        ["GameMonetize:Enabled"] = "false",
                        ["Authentication:Enabled"] = "false",
                        ["OpenTelemetry:Enabled"] = "false",
                        ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
                    });
                });
            });
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}

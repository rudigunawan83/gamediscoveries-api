using DbUp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.PostgreSQL;

public sealed class DatabaseMigrator(
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrator> logger)
{
    public void Migrate()
    {
        var connectionString = options.Value.ConnectionString;

        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
            .WithTransaction()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed");
            throw result.Error;
        }

        logger.LogInformation("Database migrations applied successfully");
    }
}

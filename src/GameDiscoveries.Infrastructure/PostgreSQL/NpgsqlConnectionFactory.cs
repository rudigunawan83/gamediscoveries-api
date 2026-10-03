using System.Data;
using GameDiscoveries.BuildingBlocks.Database;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GameDiscoveries.Infrastructure.PostgreSQL;

public sealed class NpgsqlConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

using System.Data;

namespace GameDiscoveries.BuildingBlocks.Database;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

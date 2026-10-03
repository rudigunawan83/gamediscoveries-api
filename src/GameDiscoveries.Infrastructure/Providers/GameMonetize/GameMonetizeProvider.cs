using GameDiscoveries.Infrastructure.Providers.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

/// <summary>
/// Skeleton GameMonetize provider. Full synchronization is intentionally not implemented yet.
/// </summary>
public sealed class GameMonetizeProvider(
    GameMonetizeClient client,
    IOptions<GameMonetizeOptions> options,
    ILogger<GameMonetizeProvider> logger) : IGameProvider
{
    public string Name => "GameMonetize";

    public async Task<IReadOnlyCollection<ExternalGame>> GetGamesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("GameMonetize provider is disabled");
            return [];
        }

        var feedItems = await client.FetchFeedAsync(cancellationToken);
        return feedItems
            .Select(GameMonetizeMapper.Map)
            .Where(g => g is not null)
            .Cast<ExternalGame>()
            .ToList();
    }

    public async Task<ExternalGame?> GetGameAsync(
        string providerGameId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerGameId);

        var games = await GetGamesAsync(cancellationToken);
        return games.FirstOrDefault(g =>
            string.Equals(g.ProviderGameId, providerGameId, StringComparison.OrdinalIgnoreCase));
    }
}

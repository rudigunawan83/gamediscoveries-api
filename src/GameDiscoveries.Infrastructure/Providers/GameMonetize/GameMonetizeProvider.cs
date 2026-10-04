using GameDiscoveries.BuildingBlocks.Feeds;
using GameDiscoveries.Infrastructure.Providers.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public sealed class GameMonetizeProvider(
    GameMonetizeClient client,
    IOptions<GameMonetizeOptions> options,
    ILogger<GameMonetizeProvider> logger) : IGameProvider
{
    public string Name => GameMonetizeMapper.SourceName;

    public async Task<IReadOnlyCollection<ExternalGame>> GetGamesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("GameMonetize provider is disabled");
            return [];
        }

        var feedItems = await client.FetchFeedAsync(GameFeedType.Latest, cancellationToken);
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

    public async Task<IReadOnlyCollection<string>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var games = await GetGamesAsync(cancellationToken);
        return games
            .SelectMany(g => g.Categories)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string?> GetGamePlayUrlAsync(
        string providerGameId,
        CancellationToken cancellationToken = default)
    {
        var game = await GetGameAsync(providerGameId, cancellationToken);
        return game?.EmbedUrl ?? game?.GameUrl;
    }
}

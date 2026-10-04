using GameDiscoveries.Infrastructure.Providers.Abstractions;

namespace GameDiscoveries.UnitTests.Providers;

public sealed class FakeGameProvider : IGameProvider
{
    private readonly List<ExternalGame> _games;

    public FakeGameProvider(IEnumerable<ExternalGame> games)
    {
        _games = games.ToList();
    }

    public string Name => "FakeProvider";

    public Task<IReadOnlyCollection<ExternalGame>> GetGamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ExternalGame>>(_games);

    public Task<ExternalGame?> GetGameAsync(string providerGameId, CancellationToken cancellationToken = default)
        => Task.FromResult(_games.FirstOrDefault(g => g.ProviderGameId == providerGameId));

    public Task<IReadOnlyCollection<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>(
            _games.SelectMany(g => g.Categories).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public Task<string?> GetGamePlayUrlAsync(string providerGameId, CancellationToken cancellationToken = default)
    {
        var game = _games.FirstOrDefault(g => g.ProviderGameId == providerGameId);
        return Task.FromResult(game?.EmbedUrl ?? game?.GameUrl);
    }
}

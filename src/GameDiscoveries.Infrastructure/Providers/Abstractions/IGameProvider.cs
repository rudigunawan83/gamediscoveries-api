namespace GameDiscoveries.Infrastructure.Providers.Abstractions;

public interface IGameProvider
{
    string Name { get; }

    Task<IReadOnlyCollection<ExternalGame>> GetGamesAsync(
        CancellationToken cancellationToken = default);

    Task<ExternalGame?> GetGameAsync(
        string providerGameId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<string?> GetGamePlayUrlAsync(
        string providerGameId,
        CancellationToken cancellationToken = default);
}

namespace GameDiscoveries.Modules.Leaderboards.Services;

/// <summary>
/// Future scopes (friends/category/genre) plug in here without changing public APIs.
/// </summary>
public interface ILeaderboardScopeProvider
{
    string ScopeType { get; }
    Task<IReadOnlyList<Guid>> ResolveEligibleUserIdsAsync(
        Guid? viewerUserId,
        string? scopeValue,
        CancellationToken cancellationToken = default);
}

public sealed class GlobalScopeProvider : ILeaderboardScopeProvider
{
    public string ScopeType => "GLOBAL";

    public Task<IReadOnlyList<Guid>> ResolveEligibleUserIdsAsync(
        Guid? viewerUserId,
        string? scopeValue,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Guid>>([]);
}

namespace GameDiscoveries.BuildingBlocks.Ranking;

/// <summary>
/// Abstraction for future internal ranking (views/plays/favorites/growth).
/// Current implementation is intentionally simple and replaceable.
/// </summary>
public interface IGameRankingService
{
    string OrderBySqlClause(GameRankingKind kind);
}

public enum GameRankingKind
{
    Newest = 0,
    Popular = 1,
    Trending = 2,
    Featured = 3
}

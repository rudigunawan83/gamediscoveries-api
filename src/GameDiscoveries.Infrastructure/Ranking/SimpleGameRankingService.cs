using GameDiscoveries.BuildingBlocks.Ranking;

namespace GameDiscoveries.Infrastructure.Ranking;

public sealed class SimpleGameRankingService : IGameRankingService
{
    public string OrderBySqlClause(GameRankingKind kind)
        => kind switch
        {
            GameRankingKind.Newest => "g.published_at DESC NULLS LAST, g.created_at DESC",
            GameRankingKind.Popular => "g.published_at DESC NULLS LAST, g.created_at DESC",
            GameRankingKind.Trending => "g.updated_at DESC, g.published_at DESC NULLS LAST",
            GameRankingKind.Featured => "g.published_at DESC NULLS LAST, g.title ASC",
            _ => "g.created_at DESC"
        };
}

using GameDiscoveries.Modules.Catalog.Features.ListGames;

namespace GameDiscoveries.Modules.Discovery.Features.GetHomeDiscoveries;

public static class HomeShelves
{
    public static IReadOnlyList<GameSummaryResponse> TopUp(
        IReadOnlyList<GameSummaryResponse> primary,
        IEnumerable<GameSummaryResponse> fallback,
        int limit)
    {
        var result = primary.Take(limit).ToList();
        var seen = result.Select(g => g.Id).ToHashSet();
        foreach (var game in fallback)
        {
            if (result.Count >= limit)
            {
                break;
            }

            if (seen.Add(game.Id))
            {
                result.Add(game);
            }
        }

        return result;
    }
}

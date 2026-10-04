using GameDiscoveries.Modules.Catalog.Features.ListGames;

namespace GameDiscoveries.Modules.Discovery.Features.GetHomeDiscoveries;

public sealed record HomeDiscoveriesResponse(
    IReadOnlyList<GameSummaryResponse> Featured,
    IReadOnlyList<GameSummaryResponse> Trending,
    IReadOnlyList<GameSummaryResponse> Latest,
    IReadOnlyList<GameSummaryResponse> Popular,
    IReadOnlyList<GameSummaryResponse> Mobile,
    IReadOnlyList<GameSummaryResponse> Multiplayer);

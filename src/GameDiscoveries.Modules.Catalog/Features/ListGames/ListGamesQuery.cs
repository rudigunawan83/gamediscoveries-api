using GameDiscoveries.BuildingBlocks.Pagination;

namespace GameDiscoveries.Modules.Catalog.Features.ListGames;

public sealed record ListGamesQuery : PagedRequest
{
    public string? Search { get; init; }

    public string? Category { get; init; }

    public string? Platform { get; init; }

    public bool? MobileReady { get; init; }

    public string? Sort { get; init; }

    public string? Tag { get; init; }
}

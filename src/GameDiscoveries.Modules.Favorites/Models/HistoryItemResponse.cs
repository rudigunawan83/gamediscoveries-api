using GameDiscoveries.Modules.Catalog.Features.ListGames;

namespace GameDiscoveries.Modules.Favorites.Models;

public sealed record HistoryItemResponse(
    Guid Id,
    Guid GameId,
    DateTimeOffset PlayedAt,
    int DurationSeconds,
    GameSummaryResponse Game);

using GameDiscoveries.Modules.Catalog.Features.ListGames;

namespace GameDiscoveries.Modules.Favorites.Models;

public sealed record FavoriteItemResponse(
    Guid GameId,
    DateTimeOffset FavoritedAt,
    GameSummaryResponse Game);

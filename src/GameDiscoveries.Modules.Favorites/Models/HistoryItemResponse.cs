using GameDiscoveries.Modules.Catalog.Features.ListGames;

namespace GameDiscoveries.Modules.Favorites.Models;

/// <param name="DurationSeconds">Longest single session.</param>
/// <param name="TotalPlaySeconds">Server-measured active time across all platforms.</param>
/// <param name="LastPlatform">WEB, ANDROID or IOS of the last ended session.</param>
public sealed record HistoryItemResponse(
    Guid Id,
    Guid GameId,
    DateTimeOffset PlayedAt,
    int DurationSeconds,
    long TotalPlaySeconds,
    int PlayCount,
    string? LastPlatform,
    GameSummaryResponse Game);

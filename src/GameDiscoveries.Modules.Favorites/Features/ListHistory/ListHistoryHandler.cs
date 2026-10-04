using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.BuildingBlocks.Pagination;
using GameDiscoveries.Modules.Favorites.Data;
using GameDiscoveries.Modules.Favorites.Models;

namespace GameDiscoveries.Modules.Favorites.Features.ListHistory;

public sealed class ListHistoryHandler(
    ICurrentUser currentUser,
    IUserLibraryRepository library)
{
    public async Task<(IReadOnlyList<HistoryItemResponse> Items, PaginationMeta Meta)> HandleAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await library.ListHistoryAsync(userId, safePage, safePageSize, cancellationToken);
        return (items, PaginationMeta.Create(safePage, safePageSize, total));
    }
}

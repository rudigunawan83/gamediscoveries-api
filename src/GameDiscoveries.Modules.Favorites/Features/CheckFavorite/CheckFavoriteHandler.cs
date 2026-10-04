using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Favorites.Data;

namespace GameDiscoveries.Modules.Favorites.Features.CheckFavorite;

public sealed class CheckFavoriteHandler(
    ICurrentUser currentUser,
    IUserLibraryRepository library)
{
    public async Task<bool> HandleAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        if (gameId == Guid.Empty)
        {
            throw new ValidationException("GameId is required.");
        }

        return await library.IsFavoriteAsync(userId, gameId, cancellationToken);
    }
}

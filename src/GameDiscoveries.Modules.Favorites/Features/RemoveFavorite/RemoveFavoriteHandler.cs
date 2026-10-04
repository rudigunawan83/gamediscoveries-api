using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Favorites.Data;

namespace GameDiscoveries.Modules.Favorites.Features.RemoveFavorite;

public sealed class RemoveFavoriteHandler(
    ICurrentUser currentUser,
    IUserLibraryRepository library)
{
    public async Task HandleAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        if (gameId == Guid.Empty)
        {
            throw new ValidationException("GameId is required.");
        }

        var removed = await library.RemoveFavoriteAsync(userId, gameId, cancellationToken);
        if (!removed)
        {
            throw new NotFoundException("Favorite not found", "This game is not in your favorites.");
        }
    }
}

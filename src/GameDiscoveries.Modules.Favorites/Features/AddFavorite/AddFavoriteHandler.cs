using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Favorites.Data;

namespace GameDiscoveries.Modules.Favorites.Features.AddFavorite;

public sealed record AddFavoriteRequest(Guid GameId);

public sealed class AddFavoriteHandler(
    ICurrentUser currentUser,
    IUserLibraryRepository library)
{
    public async Task HandleAsync(AddFavoriteRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        if (request.GameId == Guid.Empty)
        {
            throw new ValidationException("GameId is required.");
        }

        if (!await library.GameExistsPublishedAsync(request.GameId, cancellationToken))
        {
            throw new NotFoundException("Game not found", "The requested game does not exist or is not published.");
        }

        var added = await library.AddFavoriteAsync(userId, request.GameId, cancellationToken);
        if (!added)
        {
            throw new ConflictException(
                "Already favorited",
                "This game is already in your favorites.");
        }
    }
}

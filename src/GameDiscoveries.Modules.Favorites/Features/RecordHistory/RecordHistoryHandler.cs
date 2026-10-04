using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Favorites.Data;

namespace GameDiscoveries.Modules.Favorites.Features.RecordHistory;

public sealed record RecordHistoryRequest(Guid GameId, int? DurationSeconds);

public sealed class RecordHistoryHandler(
    ICurrentUser currentUser,
    IUserLibraryRepository library)
{
    public async Task HandleAsync(RecordHistoryRequest request, CancellationToken cancellationToken = default)
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

        await library.UpsertHistoryAsync(
            userId,
            request.GameId,
            request.DurationSeconds ?? 0,
            cancellationToken);
    }
}

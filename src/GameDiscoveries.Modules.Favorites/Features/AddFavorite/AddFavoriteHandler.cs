using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Services;
using GameDiscoveries.Modules.Favorites.Data;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Favorites.Features.AddFavorite;

public sealed record AddFavoriteRequest(Guid GameId);

public sealed class AddFavoriteHandler(
    ICurrentUser currentUser,
    IUserLibraryRepository library,
    IXpEngine xpEngine,
    IEnumerable<IMissionActivitySink> missionSinks,
    IEnumerable<IAchievementActivitySink> achievementSinks,
    IAnalyticsEventService analytics,
    ILogger<AddFavoriteHandler> logger)
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

        await xpEngine.ProcessFavoriteAddedAsync(userId, request.GameId, cancellationToken);

        foreach (var sink in missionSinks)
        {
            await sink.OnFavoriteAddedAsync(userId, request.GameId, cancellationToken);
        }

        foreach (var sink in achievementSinks)
        {
            await sink.OnFavoriteAddedAsync(userId, request.GameId, cancellationToken);
        }

        await TrackFavoriteAddedAsync(userId, request.GameId, cancellationToken);
    }

    // Recorded here rather than by clients so web and app favorites are counted once each.
    private async Task TrackFavoriteAddedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken)
    {
        try
        {
            await analytics.TrackAsync(
                new AnalyticsEventIngestRequest(
                    Guid.NewGuid(),
                    AnalyticsEventTypes.FavoriteAdded,
                    null,
                    null,
                    gameId,
                    AnalyticsSources.Api,
                    AnalyticsPlatforms.Unknown,
                    null,
                    null,
                    null,
                    null,
                    null,
                    DateTimeOffset.UtcNow),
                userId,
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record FAVORITE_ADDED for game {GameId}", gameId);
        }
    }
}

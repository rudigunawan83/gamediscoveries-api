using FluentAssertions;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Favorites.Data;
using GameDiscoveries.Modules.Favorites.Models;
using GameDiscoveries.Modules.Favorites.Processing;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameDiscoveries.UnitTests.Favorites;

public sealed class GameSessionHistoryEventHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Session_end_records_history_from_the_session_row()
    {
        var repository = new FakeRepository();
        var handler = new GameSessionHistoryEventHandler(repository, NullLogger<GameSessionHistoryEventHandler>.Instance);

        await handler.HandleAsync(Command(AnalyticsEventTypes.GameSessionEnd, UserId, "s-1"));

        repository.Recorded.Should().Equal((UserId, "s-1"));
    }

    [Theory]
    [InlineData(AnalyticsEventTypes.GameSessionHeartbeat, true, "s-1")]
    [InlineData(AnalyticsEventTypes.GameSessionEnd, false, "s-1")]
    [InlineData(AnalyticsEventTypes.GameSessionEnd, true, " ")]
    public async Task Ignores_other_events_guests_and_missing_sessions(string eventType, bool signedIn, string sessionId)
    {
        var repository = new FakeRepository();
        var handler = new GameSessionHistoryEventHandler(repository, NullLogger<GameSessionHistoryEventHandler>.Instance);

        await handler.HandleAsync(Command(eventType, signedIn ? UserId : null, sessionId));

        repository.Recorded.Should().BeEmpty();
    }

    [Fact]
    public async Task Repository_failures_do_not_break_event_dispatch()
    {
        var repository = new FakeRepository { Throw = true };
        var handler = new GameSessionHistoryEventHandler(repository, NullLogger<GameSessionHistoryEventHandler>.Instance);

        var act = () => handler.HandleAsync(Command(AnalyticsEventTypes.GameSessionEnd, UserId, "s-1"));

        await act.Should().NotThrowAsync();
    }

    private static AnalyticsEventWriteCommand Command(string eventType, Guid? userId, string sessionId) =>
        new(
            Guid.NewGuid(),
            eventType,
            userId,
            null,
            sessionId,
            Guid.NewGuid(),
            "MOBILE_APP",
            "ANDROID",
            null,
            null,
            null,
            null,
            // Client-supplied metadata must not influence recorded play time.
            new Dictionary<string, object?> { ["activeSeconds"] = 999_999 },
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class FakeRepository : IUserLibraryRepository
    {
        public List<(Guid UserId, string SessionId)> Recorded { get; } = [];
        public bool Throw { get; init; }

        public Task<bool> RecordEndedSessionAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException("db down");
            Recorded.Add((userId, sessionId));
            return Task.FromResult(true);
        }

        public Task<bool> GameExistsPublishedAsync(Guid gameId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<FavoriteItemResponse> Items, long Total)> ListFavoritesAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AddFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveFavoriteAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<HistoryItemResponse> Items, long Total)> ListHistoryAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertHistoryAsync(Guid userId, Guid gameId, int durationSeconds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

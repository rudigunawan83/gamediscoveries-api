using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Data;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Options;
using GameDiscoveries.Modules.Analytics.Processing;
using GameDiscoveries.Modules.Analytics.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.UnitTests.Analytics;

public sealed class AnalyticsEventServiceTests
{
    [Fact]
    public async Task TrackAsync_is_idempotent_for_duplicate_event_id()
    {
        var store = new FakeStore();
        var service = CreateService(store);
        var eventId = Guid.NewGuid();
        var request = PageView(eventId);

        var first = await service.TrackAsync(request, null, "1.1.1.1", "ua");
        var second = await service.TrackAsync(request, null, "1.1.1.1", "ua");

        first.Status.Should().Be("accepted");
        second.Status.Should().Be("duplicate");
        store.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task TrackBatchAsync_counts_accepted_duplicates_and_rejects()
    {
        var store = new FakeStore();
        var service = CreateService(store);
        var dup = Guid.NewGuid();
        await service.TrackAsync(PageView(dup), null, null, null);

        var result = await service.TrackBatchAsync(
            [
                PageView(dup),
                PageView(Guid.NewGuid()),
                new AnalyticsEventIngestRequest(
                    Guid.NewGuid(),
                    "NOT_REAL",
                    Guid.NewGuid(),
                    null,
                    null,
                    "WEB",
                    "WEB",
                    null,
                    null,
                    null,
                    null,
                    null,
                    DateTimeOffset.UtcNow)
            ],
            null,
            null,
            null);

        result.Accepted.Should().Be(1);
        result.Duplicates.Should().Be(1);
        result.Rejected.Should().Be(1);
    }

    [Fact]
    public async Task TrackAsync_rejects_unknown_game()
    {
        var store = new FakeStore { GamesExist = false };
        var service = CreateService(store);
        var act = () => service.TrackAsync(
            new AnalyticsEventIngestRequest(
                Guid.NewGuid(),
                "GAME_VIEW",
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow),
            null,
            null,
            null);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task TrackBatchAsync_rejects_oversized_batch()
    {
        var service = CreateService(new FakeStore());
        var events = Enumerable.Range(0, 101).Select(_ => PageView(Guid.NewGuid())).ToList();
        var act = () => service.TrackBatchAsync(events, null, null, null);
        await act.Should().ThrowAsync<ValidationException>();
    }

    private static AnalyticsEventIngestRequest PageView(Guid eventId) =>
        new(
            eventId,
            "PAGE_VIEW",
            Guid.NewGuid(),
            "session",
            null,
            "WEB",
            "WEB",
            null,
            null,
            "/",
            null,
            null,
            DateTimeOffset.UtcNow);

    private static AnalyticsEventService CreateService(FakeStore store) =>
        new(
            store,
            new AnalyticsEventValidator(Options.Create(new AnalyticsOptions
            {
                MaxBatchSize = 100,
                MaxMetadataSizeKb = 16,
                EnableAnonymousTracking = true,
                ValidateGameExists = true
            })),
            new AnalyticsEventDispatcher(Array.Empty<IAnalyticsEventHandler>()),
            Options.Create(new AnalyticsOptions
            {
                MaxBatchSize = 100,
                MaxMetadataSizeKb = 16,
                EnableAnonymousTracking = true,
                ValidateGameExists = true
            }),
            NullLogger<AnalyticsEventService>.Instance);

    private sealed class FakeStore : IAnalyticsEventStore
    {
        public List<AnalyticsEventWriteCommand> Events { get; } = [];
        public bool GamesExist { get; set; } = true;

        public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Events.Any(e => e.EventId == eventId));

        public Task<IReadOnlySet<Guid>> GetExistingEventIdsAsync(
            IReadOnlyCollection<Guid> eventIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(Events.Select(e => e.EventId).Where(eventIds.Contains).ToHashSet());

        public Task InsertAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
        {
            Events.Add(command);
            return Task.CompletedTask;
        }

        public Task<int> TryInsertAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
        {
            if (Events.Any(e => e.EventId == command.EventId))
            {
                return Task.FromResult(0);
            }

            Events.Add(command);
            return Task.FromResult(1);
        }

        public Task InsertManyAsync(
            IReadOnlyList<AnalyticsEventWriteCommand> commands,
            CancellationToken cancellationToken = default)
        {
            Events.AddRange(commands);
            return Task.CompletedTask;
        }

        public Task UpsertSessionAsync(
            string sessionId,
            Guid? userId,
            Guid? anonymousId,
            string source,
            string platform,
            DateTimeOffset activityAt,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LinkIdentityAsync(Guid anonymousId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> GameExistsAsync(Guid gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GamesExist);

        public Task<IReadOnlySet<Guid>> GetExistingGameIdsAsync(
            IReadOnlyCollection<Guid> gameIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(GamesExist ? gameIds.ToHashSet() : new HashSet<Guid>());

        public Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AnalyticsOverviewResponse(0, 0, 0, 0, 0, 0, [], []));

        public Task TrackLegacyAsync(
            string eventName,
            Guid? userId,
            string? sessionId,
            Guid? gameId,
            IReadOnlyDictionary<string, object?> properties,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

using FluentAssertions;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Services;
using GameDiscoveries.Modules.Xp.Data;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Options;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.UnitTests.Xp;

public sealed class SessionMilestoneEvaluatorTests
{
    [Theory]
    [InlineData(29, null)]
    [InlineData(119, null)]
    [InlineData(120, XpRuleCodes.SessionMilestone2M)]
    [InlineData(299, XpRuleCodes.SessionMilestone2M)]
    [InlineData(300, XpRuleCodes.SessionMilestone5M)]
    [InlineData(599, XpRuleCodes.SessionMilestone5M)]
    [InlineData(600, XpRuleCodes.SessionMilestone10M)]
    public void Selects_only_highest_milestone(int activeSeconds, string? expected)
    {
        SessionMilestoneEvaluator.SelectHighestMilestoneRule(activeSeconds).Should().Be(expected);
    }
}

public sealed class XpEngineTests
{
    [Fact]
    public async Task Awards_session_bundle_for_first_valid_play()
    {
        var store = new FakeXpStore();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("D");

        store.Sessions[sessionId] = new SessionXpSnapshot(sessionId, userId, gameId, 45, 50, true, "ENDED");
        store.Categories[gameId] = [categoryId];

        var engine = CreateEngine(store);
        var result = await engine.ProcessGameSessionEndAsync(sessionId);

        result.XpAwarded.Should().Be(65); // 20+15+20+10
        result.Transactions.Select(t => t.RuleCode).Should().BeEquivalentTo(
        [
            XpRuleCodes.FirstGameDiscovery,
            XpRuleCodes.NewGameDiscovered,
            XpRuleCodes.NewGenreDiscovered,
            XpRuleCodes.ValidGameSession
        ]);
    }

    [Fact]
    public async Task Awards_only_highest_milestone_on_replay()
    {
        var store = new FakeXpStore();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("D");

        store.PriorValidSessions.Add((userId, gameId));
        store.Sessions[sessionId] = new SessionXpSnapshot(sessionId, userId, gameId, 360, 480, true, "ENDED");

        var engine = CreateEngine(store);
        var result = await engine.ProcessGameSessionEndAsync(sessionId);

        result.XpAwarded.Should().Be(30); // 10 + 20
        result.Transactions.Select(t => t.RuleCode).Should().BeEquivalentTo(
        [
            XpRuleCodes.ValidGameSession,
            XpRuleCodes.SessionMilestone5M
        ]);
    }

    [Fact]
    public async Task Invalid_session_awards_zero()
    {
        var store = new FakeXpStore();
        var sessionId = Guid.NewGuid().ToString("D");
        store.Sessions[sessionId] = new SessionXpSnapshot(sessionId, Guid.NewGuid(), Guid.NewGuid(), 10, 10, false, "ENDED");

        var result = await CreateEngine(store).ProcessGameSessionEndAsync(sessionId);

        result.XpAwarded.Should().Be(0);
        result.Reason.Should().Be("SESSION_NOT_VALID");
    }

    [Fact]
    public async Task Anonymous_session_awards_zero()
    {
        var store = new FakeXpStore();
        var sessionId = Guid.NewGuid().ToString("D");
        store.Sessions[sessionId] = new SessionXpSnapshot(sessionId, null, Guid.NewGuid(), 120, 120, true, "ENDED");

        var result = await CreateEngine(store).ProcessGameSessionEndAsync(sessionId);
        result.Reason.Should().Be("ANONYMOUS_NO_XP");
        result.XpAwarded.Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_award_is_idempotent()
    {
        var store = new FakeXpStore();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var engine = CreateEngine(store);

        var first = await engine.ProcessFavoriteAddedAsync(userId, gameId);
        var second = await engine.ProcessFavoriteAddedAsync(userId, gameId);

        first.XpAwarded.Should().Be(10);
        second.XpAwarded.Should().Be(0);
        second.Reason.Should().Be("ALREADY_REWARDED");
        store.TotalXp[userId].Should().Be(10);
    }

    [Fact]
    public async Task Daily_cap_blocks_further_awards()
    {
        var store = new FakeXpStore { DailyCapOverride = 10 };
        var userId = Guid.NewGuid();
        var engine = CreateEngine(store, dailyCap: 10);

        await engine.ProcessFavoriteAddedAsync(userId, Guid.NewGuid());
        var second = await engine.ProcessFavoriteAddedAsync(userId, Guid.NewGuid());

        second.XpAwarded.Should().Be(0);
        second.Reason.Should().Be("DAILY_XP_CAP_REACHED");
    }

    private static XpEngine CreateEngine(FakeXpStore store, int dailyCap = 500) =>
        new(
            store,
            new XpRuleCatalog(Options.Create(new XpOptions())),
            Options.Create(new XpOptions { Enabled = true, DailyXpCap = dailyCap }),
            new NoopAnalytics(),
            [],
            [],
            NullLogger<XpEngine>.Instance);

    private sealed class NoopAnalytics : IAnalyticsEventService
    {
        public Task<AnalyticsEventIngestResult> TrackAsync(
            AnalyticsEventIngestRequest request,
            Guid? authenticatedUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AnalyticsEventIngestResult(request.EventId ?? Guid.NewGuid(), "accepted"));

        public Task<AnalyticsBatchIngestResult> TrackBatchAsync(
            IReadOnlyList<AnalyticsEventIngestRequest> events,
            Guid? authenticatedUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AnalyticsBatchIngestResult(0, 0, 0, []));

        public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AnalyticsOverviewResponse(0, 0, 0, 0, 0, 0, [], []));
    }

    private sealed class FakeXpStore : IXpStore
    {
        public Dictionary<string, SessionXpSnapshot> Sessions { get; } = new();
        public Dictionary<Guid, List<Guid>> Categories { get; } = new();
        public HashSet<(Guid UserId, Guid GameId)> PriorValidSessions { get; } = [];
        public Dictionary<Guid, long> TotalXp { get; } = new();
        public HashSet<string> RewardedKeys { get; } = new(StringComparer.Ordinal);
        public int DailyCapOverride { get; set; } = 500;
        private int _earnedToday;

        public Task<XpAwardResult> TryAwardAsync(XpAwardRequest request, int dailyCap, CancellationToken cancellationToken = default)
        {
            var key = $"{request.UserId}:{request.RuleCode}:{request.ReferenceType}:{request.ReferenceId}";
            if (!RewardedKeys.Add(key))
            {
                return Task.FromResult(new XpAwardResult(true, 0, TotalXp.GetValueOrDefault(request.UserId), "ALREADY_REWARDED", []));
            }

            if (request.XpAmount > 0 && _earnedToday + request.XpAmount > dailyCap)
            {
                return Task.FromResult(new XpAwardResult(true, 0, TotalXp.GetValueOrDefault(request.UserId), "DAILY_XP_CAP_REACHED", []));
            }

            if (request.XpAmount > 0)
            {
                _earnedToday += request.XpAmount;
            }

            TotalXp[request.UserId] = TotalXp.GetValueOrDefault(request.UserId) + request.XpAmount;
            return Task.FromResult(new XpAwardResult(
                true,
                request.XpAmount,
                TotalXp[request.UserId],
                null,
                [new XpAwardedItem(request.RuleCode, request.XpAmount)]));
        }

        public Task<UserProgressEntity> GetOrCreateProgressAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserProgressEntity { UserId = userId, TotalXp = TotalXp.GetValueOrDefault(userId), Level = 1 });

        public Task<IReadOnlyList<XpTransactionDto>> GetTransactionsAsync(
            Guid userId, int limit, int offset, string? ruleCode, DateTimeOffset? from, DateTimeOffset? to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XpTransactionDto>>([]);

        public Task<int> GetXpEarnedTodayAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_earnedToday);

        public Task<bool> HasValidSessionBeforeAsync(Guid userId, string? excludeSessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PriorValidSessions.Any(x => x.UserId == userId));

        public Task<bool> HasValidSessionForGameAsync(
            Guid userId, Guid gameId, string? excludeSessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PriorValidSessions.Contains((userId, gameId)));

        public Task<IReadOnlyList<Guid>> GetGameCategoryIdsAsync(Guid gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Categories.GetValueOrDefault(gameId) ?? []);

        public Task<bool> HasValidSessionForCategoryAsync(
            Guid userId, Guid categoryId, string? excludeSessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PriorValidSessions.Any(x => x.UserId == userId));

        public Task<SessionXpSnapshot?> GetSessionSnapshotAsync(string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.GetValueOrDefault(sessionId));

        public Task<XpTransactionEntity?> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<XpTransactionEntity?>(null);

        public Task<int> CountTransactionsAsync(
            Guid userId, string? ruleCode, DateTimeOffset? from, DateTimeOffset? to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task ApplyLevelAsync(Guid userId, int level, long currentLevelXp, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InsertLevelHistoryAsync(
            Guid userId, int oldLevel, int newLevel, long totalXp, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

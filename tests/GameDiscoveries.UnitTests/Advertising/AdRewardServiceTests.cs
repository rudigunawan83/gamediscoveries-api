using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Advertising.Data;
using GameDiscoveries.Modules.Advertising.Models;
using GameDiscoveries.Modules.Advertising.Options;
using GameDiscoveries.Modules.Advertising.Services;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Options;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.UnitTests.Advertising;

public sealed class AdRewardServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Rewarded_ad_xp_beats_the_best_single_game_session()
    {
        var o = new XpOptions();

        o.RewardedAdXp.Should().BeGreaterThan(o.ValidGameSessionXp + o.SessionMilestone10MinutesXp);
    }

    [Fact]
    public async Task Verified_callback_grants_ad_xp_once_per_ticket()
    {
        var store = new FakeStore();
        var ticket = store.Add(UserId);
        var xp = new FakeXpEngine();
        var service = Service(store, xp, Callback(ticket.Token, "tx-1", UserId));

        var outcome = await service.HandleAdMobCallbackAsync("?signed");

        outcome.Should().Be(new AdMobCallbackOutcome(true, "REWARDED", 50));
        xp.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId,
            RuleCode = XpRuleCodes.RewardedAdWatched,
            ReferenceType = XpReferenceTypes.AdReward,
            ReferenceId = ticket.Id.ToString("D"),
            XpAmount = 50
        });
        store.Awards.Should().Equal((ticket.Id, 50, (string?)null));
    }

    [Fact]
    public async Task Forged_callback_grants_nothing()
    {
        var store = new FakeStore();
        store.Add(UserId);
        var xp = new FakeXpEngine();

        var outcome = await Service(store, xp, callback: null).HandleAdMobCallbackAsync("?forged");

        outcome.SignatureValid.Should().BeFalse();
        xp.Requests.Should().BeEmpty();
        store.Claims.Should().BeEmpty();
    }

    [Fact]
    public async Task Callback_for_another_user_is_rejected()
    {
        var store = new FakeStore();
        var ticket = store.Add(UserId);
        var xp = new FakeXpEngine();

        var outcome = await Service(store, xp, Callback(ticket.Token, "tx-1", Guid.NewGuid()))
            .HandleAdMobCallbackAsync("?signed");

        outcome.Result.Should().Be("USER_MISMATCH");
        store.Rejections.Should().Equal((ticket.Id, "USER_MISMATCH"));
        xp.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AdRewardClaimResult.DailyLimitReached, "DAILY_LIMIT_REACHED")]
    [InlineData(AdRewardClaimResult.Expired, "EXPIRED")]
    [InlineData(AdRewardClaimResult.DuplicateTransaction, "DUPLICATE_TRANSACTION")]
    [InlineData(AdRewardClaimResult.NotPending, "ALREADY_PROCESSED")]
    public async Task Unclaimable_tickets_grant_nothing(AdRewardClaimResult claim, string expected)
    {
        var store = new FakeStore { ClaimResult = claim };
        var ticket = store.Add(UserId);
        var xp = new FakeXpEngine();

        var outcome = await Service(store, xp, Callback(ticket.Token, "tx-1", UserId))
            .HandleAdMobCallbackAsync("?signed");

        outcome.Should().Be(new AdMobCallbackOutcome(true, expected));
        xp.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_ticket_and_disabled_feature_grant_nothing()
    {
        var store = new FakeStore();
        var ticket = store.Add(UserId);
        var xp = new FakeXpEngine();

        (await Service(store, xp, Callback("other", "tx-1", UserId)).HandleAdMobCallbackAsync("?s"))
            .Result.Should().Be("UNKNOWN_TICKET");
        (await Service(store, xp, Callback(ticket.Token, "tx-1", UserId), enabled: false).HandleAdMobCallbackAsync("?s"))
            .Result.Should().Be("DISABLED");
        xp.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Ticket_is_refused_when_disabled_or_daily_limit_is_used()
    {
        var store = new FakeStore();

        await FluentActions.Awaiting(() => Service(store, new FakeXpEngine(), null, enabled: false)
                .CreateTicketAsync(UserId, "ANDROID"))
            .Should().ThrowAsync<ServiceUnavailableException>();

        store.RewardedToday = 5;
        await FluentActions.Awaiting(() => Service(store, new FakeXpEngine(), null)
                .CreateTicketAsync(UserId, "ANDROID"))
            .Should().ThrowAsync<TooManyRequestsException>();
    }

    [Fact]
    public async Task Ticket_carries_user_and_unguessable_custom_data()
    {
        var store = new FakeStore { RewardedToday = 2 };

        var ticket = await Service(store, new FakeXpEngine(), null).CreateTicketAsync(UserId, "ios");

        ticket.UserId.Should().Be(UserId.ToString("D"));
        ticket.CustomData.Should().HaveLength(32);
        ticket.XpReward.Should().Be(50);
        ticket.RemainingToday.Should().Be(3);
        store.Inserted.Should().ContainSingle().Which.Platform.Should().Be("IOS");
    }

    private static AdMobCallback Callback(string token, string transactionId, Guid userId) =>
        new(transactionId, token, userId.ToString("D"), "unit-1", "1");

    private static AdRewardService Service(
        FakeStore store,
        FakeXpEngine xp,
        AdMobCallback? callback,
        bool enabled = true) =>
        new(
            store,
            new FakeVerifier(callback),
            xp,
            new XpRuleCatalog(Options.Create(new XpOptions())),
            Options.Create(new AdRewardOptions { Enabled = enabled, DailyLimit = 5 }),
            NullLogger<AdRewardService>.Instance);

    private sealed class FakeVerifier(AdMobCallback? callback) : IAdMobSsvVerifier
    {
        public Task<AdMobCallback?> VerifyAsync(string rawQuery, CancellationToken cancellationToken = default) =>
            Task.FromResult(callback);
    }

    private sealed class FakeStore : IAdRewardStore
    {
        private readonly List<AdRewardTicket> _tickets = [];

        public AdRewardClaimResult ClaimResult { get; init; } = AdRewardClaimResult.Claimed;
        public int RewardedToday { get; set; }
        public List<Guid> Claims { get; } = [];
        public List<(Guid, int, string?)> Awards { get; } = [];
        public List<(Guid, string)> Rejections { get; } = [];
        public List<(Guid Id, string? Platform)> Inserted { get; } = [];

        public AdRewardTicket Add(Guid userId)
        {
            var ticket = new AdRewardTicket(
                Guid.NewGuid(), userId, "tok-" + _tickets.Count, AdRewardStatuses.Pending, 0, null, null,
                DateTimeOffset.UtcNow.AddHours(1));
            _tickets.Add(ticket);
            return ticket;
        }

        public Task<int> CountRewardedTodayAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RewardedToday);

        public Task<int> CountTicketsTodayAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tickets.Count);

        public Task<DateTimeOffset> InsertTicketAsync(
            Guid id, Guid userId, string token, string? platform, int ttlMinutes,
            CancellationToken cancellationToken = default)
        {
            Inserted.Add((id, platform));
            return Task.FromResult(DateTimeOffset.UtcNow.AddMinutes(ttlMinutes));
        }

        public Task<AdRewardTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tickets.FirstOrDefault(t => t.Id == id));

        public Task<AdRewardTicket?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tickets.FirstOrDefault(t => t.Token == token));

        public Task<AdRewardClaimResult> TryClaimAsync(
            Guid ticketId, Guid userId, string transactionId, string? adUnit, int dailyLimit,
            CancellationToken cancellationToken = default)
        {
            Claims.Add(ticketId);
            return Task.FromResult(ClaimResult);
        }

        public Task SetAwardAsync(Guid ticketId, int xpAwarded, string? reason, CancellationToken cancellationToken = default)
        {
            Awards.Add((ticketId, xpAwarded, reason));
            return Task.CompletedTask;
        }

        public Task RejectAsync(Guid ticketId, string reason, CancellationToken cancellationToken = default)
        {
            Rejections.Add((ticketId, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeXpEngine : IXpEngine
    {
        public List<XpAwardRequest> Requests { get; } = [];

        public Task<XpAwardResult> AwardAsync(XpAwardRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new XpAwardResult(true, request.XpAmount, request.XpAmount, null, []));
        }

        public Task<XpAwardResult> ProcessGameSessionEndAsync(string sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XpAwardResult> ProcessFavoriteAddedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XpAwardResult> ProcessRatingCreatedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XpAwardResult> ProcessReviewCreatedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XpAwardResult> AdminAdjustAsync(
            Guid userId, Guid adminId, int xpAmount, string description, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XpAwardResult> ReverseAsync(
            Guid transactionId, Guid adminId, string? reason, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserXpSummary> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<XpTransactionDto>> GetHistoryAsync(
            Guid userId, int limit, int offset, string? ruleCode, DateTimeOffset? from, DateTimeOffset? to,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

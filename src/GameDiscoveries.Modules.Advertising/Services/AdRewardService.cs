using System.Security.Cryptography;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Advertising.Data;
using GameDiscoveries.Modules.Advertising.Models;
using GameDiscoveries.Modules.Advertising.Options;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Advertising.Services;

public interface IAdRewardService
{
    Task<AdRewardStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AdRewardTicketCreatedResponse> CreateTicketAsync(
        Guid userId,
        string? platform,
        CancellationToken cancellationToken = default);

    Task<AdRewardTicketResponse> GetTicketAsync(
        Guid userId,
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<AdMobCallbackOutcome> HandleAdMobCallbackAsync(
        string rawQuery,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// XP for rewarded video ads. The client only obtains a ticket; XP is granted
/// solely from a signature-verified AdMob SSV callback carrying that ticket.
/// </summary>
public sealed class AdRewardService(
    IAdRewardStore store,
    IAdMobSsvVerifier verifier,
    IXpEngine xp,
    IXpRuleCatalog rules,
    IOptions<AdRewardOptions> options,
    ILogger<AdRewardService> logger) : IAdRewardService
{
    private const int TicketsPerRewardAllowance = 4;

    private static readonly HashSet<string> Platforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "ANDROID",
        "IOS"
    };

    public async Task<AdRewardStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var xpPerAd = rules.ResolveAmount(XpRuleCodes.RewardedAdWatched);
        if (!opts.Enabled || xpPerAd <= 0)
        {
            return new AdRewardStatusResponse(false, xpPerAd, opts.DailyLimit, 0, 0);
        }

        var watched = await store.CountRewardedTodayAsync(userId, cancellationToken);
        return new AdRewardStatusResponse(
            true,
            xpPerAd,
            opts.DailyLimit,
            watched,
            Math.Max(0, opts.DailyLimit - watched));
    }

    public async Task<AdRewardTicketCreatedResponse> CreateTicketAsync(
        Guid userId,
        string? platform,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(userId, cancellationToken);
        if (!status.Enabled)
        {
            throw new ServiceUnavailableException("Rewarded ads are not available.");
        }

        if (status.RemainingToday <= 0)
        {
            throw new TooManyRequestsException("Daily rewarded ad limit reached. Come back tomorrow.");
        }

        var issuedToday = await store.CountTicketsTodayAsync(userId, cancellationToken);
        if (issuedToday >= status.DailyLimit * TicketsPerRewardAllowance)
        {
            throw new TooManyRequestsException("Too many rewarded ad attempts today.");
        }

        var normalizedPlatform = platform?.Trim().ToUpperInvariant();
        if (normalizedPlatform is not null && !Platforms.Contains(normalizedPlatform))
        {
            throw new ValidationException("Unsupported platform.");
        }

        var ticketId = Guid.NewGuid();
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        var expiresAt = await store.InsertTicketAsync(
            ticketId,
            userId,
            token,
            normalizedPlatform,
            options.Value.TicketTtlMinutes,
            cancellationToken);

        return new AdRewardTicketCreatedResponse(
            ticketId,
            token,
            userId.ToString("D"),
            status.XpPerAd,
            expiresAt,
            status.RemainingToday);
    }

    public async Task<AdRewardTicketResponse> GetTicketAsync(
        Guid userId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await store.GetByIdAsync(ticketId, cancellationToken);
        if (ticket is null || ticket.UserId != userId)
        {
            throw new NotFoundException("Ticket not found", "Rewarded ad ticket was not found.");
        }

        return new AdRewardTicketResponse(ticket.Id, ticket.Status, ticket.XpAwarded, ticket.Reason);
    }

    public async Task<AdMobCallbackOutcome> HandleAdMobCallbackAsync(
        string rawQuery,
        CancellationToken cancellationToken = default)
    {
        var callback = await verifier.VerifyAsync(rawQuery, cancellationToken);
        if (callback is null)
        {
            logger.LogWarning("admob_ssv_rejected reason=INVALID_SIGNATURE");
            return new AdMobCallbackOutcome(false, "INVALID_SIGNATURE");
        }

        var opts = options.Value;
        if (!opts.Enabled)
        {
            return Outcome("DISABLED");
        }

        // Console "verify URL" pings and unrelated ad units carry no ticket.
        if (string.IsNullOrEmpty(callback.CustomData) || string.IsNullOrEmpty(callback.TransactionId))
        {
            return Outcome("NO_TICKET");
        }

        var ticket = await store.GetByTokenAsync(callback.CustomData, cancellationToken);
        if (ticket is null)
        {
            return Outcome("UNKNOWN_TICKET");
        }

        if (callback.UserId is not null
            && !string.Equals(callback.UserId, ticket.UserId.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            await store.RejectAsync(ticket.Id, "USER_MISMATCH", cancellationToken);
            return Outcome("USER_MISMATCH", ticket);
        }

        if (opts.AllowedAdUnits.Length > 0
            && (callback.AdUnit is null || !opts.AllowedAdUnits.Contains(callback.AdUnit, StringComparer.Ordinal)))
        {
            await store.RejectAsync(ticket.Id, "AD_UNIT_NOT_ALLOWED", cancellationToken);
            return Outcome("AD_UNIT_NOT_ALLOWED", ticket);
        }

        var claim = await store.TryClaimAsync(
            ticket.Id,
            ticket.UserId,
            callback.TransactionId,
            callback.AdUnit,
            opts.DailyLimit,
            cancellationToken);

        switch (claim)
        {
            case AdRewardClaimResult.Claimed:
                break;
            case AdRewardClaimResult.AlreadyClaimedSameTransaction:
                // AdMob retries until it gets a 200; finish an award that may have failed midway.
                if (ticket.Status != AdRewardStatuses.Rewarded && ticket.Status != AdRewardStatuses.Pending)
                {
                    return Outcome("ALREADY_PROCESSED", ticket);
                }

                break;
            default:
                return Outcome(claim switch
                {
                    AdRewardClaimResult.Expired => "EXPIRED",
                    AdRewardClaimResult.DailyLimitReached => "DAILY_LIMIT_REACHED",
                    AdRewardClaimResult.DuplicateTransaction => "DUPLICATE_TRANSACTION",
                    _ => "ALREADY_PROCESSED"
                }, ticket);
        }

        var result = await xp.AwardAsync(
            new XpAwardRequest(
                ticket.UserId,
                XpRuleCodes.RewardedAdWatched,
                "REWARDED_AD_COMPLETED",
                XpReferenceTypes.AdReward,
                ticket.Id.ToString("D"),
                rules.ResolveAmount(XpRuleCodes.RewardedAdWatched),
                "Rewarded video watched",
                new Dictionary<string, object?>
                {
                    ["adUnit"] = callback.AdUnit,
                    ["transactionId"] = callback.TransactionId
                }),
            cancellationToken);

        if (result.XpAwarded > 0 || result.Reason != "ALREADY_REWARDED")
        {
            await store.SetAwardAsync(ticket.Id, result.XpAwarded, result.Reason, cancellationToken);
        }

        logger.LogInformation(
            "admob_ssv_rewarded userId={UserId} ticketId={TicketId} xp={Xp} reason={Reason}",
            ticket.UserId,
            ticket.Id,
            result.XpAwarded,
            result.Reason);
        return new AdMobCallbackOutcome(true, "REWARDED", result.XpAwarded);
    }

    private AdMobCallbackOutcome Outcome(string result, AdRewardTicket? ticket = null)
    {
        logger.LogInformation(
            "admob_ssv_ignored reason={Reason} ticketId={TicketId}",
            result,
            ticket?.Id);
        return new AdMobCallbackOutcome(true, result);
    }
}

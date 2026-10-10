namespace GameDiscoveries.Modules.Advertising.Models;

public static class AdRewardStatuses
{
    public const string Pending = "PENDING";
    public const string Rewarded = "REWARDED";
    public const string Rejected = "REJECTED";
}

public sealed record AdRewardStatusResponse(
    bool Enabled,
    int XpPerAd,
    int DailyLimit,
    int WatchedToday,
    int RemainingToday);

public sealed record CreateAdRewardTicketRequest(string? Platform);

/// <summary>
/// Pass <see cref="CustomData"/> and <see cref="UserId"/> to the AdMob SDK
/// server-side verification options before showing the rewarded ad.
/// </summary>
public sealed record AdRewardTicketCreatedResponse(
    Guid TicketId,
    string CustomData,
    string UserId,
    int XpReward,
    DateTimeOffset ExpiresAt,
    int RemainingToday);

public sealed record AdRewardTicketResponse(
    Guid TicketId,
    string Status,
    int XpAwarded,
    string? Reason);

public sealed record AdRewardTicket(
    Guid Id,
    Guid UserId,
    string Token,
    string Status,
    int XpAwarded,
    string? Reason,
    string? TransactionId,
    DateTimeOffset ExpiresAt);

public enum AdRewardClaimResult
{
    Claimed,
    AlreadyClaimedSameTransaction,
    NotPending,
    Expired,
    DailyLimitReached,
    DuplicateTransaction
}

/// <param name="SignatureValid">False means the callback did not come from AdMob.</param>
/// <param name="Result">Machine-readable outcome, for logs and tests.</param>
public sealed record AdMobCallbackOutcome(bool SignatureValid, string Result, int XpAwarded = 0);

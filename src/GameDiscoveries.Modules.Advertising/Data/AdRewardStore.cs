using System.Data;
using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Advertising.Models;

namespace GameDiscoveries.Modules.Advertising.Data;

public interface IAdRewardStore
{
    Task<int> CountRewardedTodayAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountTicketsTodayAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DateTimeOffset> InsertTicketAsync(
        Guid id,
        Guid userId,
        string token,
        string? platform,
        int ttlMinutes,
        CancellationToken cancellationToken = default);

    Task<AdRewardTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdRewardTicket?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically moves a PENDING ticket to REWARDED for <paramref name="transactionId"/>,
    /// enforcing expiry, the per-user daily limit and transaction uniqueness.
    /// </summary>
    Task<AdRewardClaimResult> TryClaimAsync(
        Guid ticketId,
        Guid userId,
        string transactionId,
        string? adUnit,
        int dailyLimit,
        CancellationToken cancellationToken = default);

    Task SetAwardAsync(Guid ticketId, int xpAwarded, string? reason, CancellationToken cancellationToken = default);

    Task RejectAsync(Guid ticketId, string reason, CancellationToken cancellationToken = default);
}

public sealed class AdRewardStore(IDbConnectionFactory connectionFactory) : IAdRewardStore
{
    private const string TicketSelect = """
        SELECT id AS Id, user_id AS UserId, token AS Token, status AS Status,
               xp_awarded AS XpAwarded, reason AS Reason, transaction_id AS TransactionId,
               expires_at AS ExpiresAt
        FROM ad_reward_tickets
        """;

    public async Task<int> CountRewardedTodayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int FROM ad_reward_tickets
            WHERE user_id = @UserId AND status = 'REWARDED'
              AND completed_at >= date_trunc('day', NOW() AT TIME ZONE 'utc')
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> CountTicketsTodayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int FROM ad_reward_tickets
            WHERE user_id = @UserId
              AND created_at >= date_trunc('day', NOW() AT TIME ZONE 'utc')
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<DateTimeOffset> InsertTicketAsync(
        Guid id,
        Guid userId,
        string token,
        string? platform,
        int ttlMinutes,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<DateTimeOffset>(new CommandDefinition(
            """
            INSERT INTO ad_reward_tickets (id, user_id, token, platform, status, created_at, expires_at)
            VALUES (@Id, @UserId, @Token, @Platform, 'PENDING', (NOW() AT TIME ZONE 'utc'),
                    (NOW() AT TIME ZONE 'utc') + make_interval(mins => @TtlMinutes))
            RETURNING expires_at
            """,
            new { Id = id, UserId = userId, Token = token, Platform = platform, TtlMinutes = ttlMinutes },
            cancellationToken: cancellationToken));
    }

    public async Task<AdRewardTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            TicketSelect + " WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
        return row?.ToTicket();
    }

    public async Task<AdRewardTicket?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            TicketSelect + " WHERE token = @Token",
            new { Token = token },
            cancellationToken: cancellationToken));
        return row?.ToTicket();
    }

    public async Task<AdRewardClaimResult> TryClaimAsync(
        Guid ticketId,
        Guid userId,
        string transactionId,
        string? adUnit,
        int dailyLimit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Serialises claims per user so the daily limit cannot be raced.
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtext(@Key))",
            new { Key = "ad_reward:" + userId.ToString("D") },
            transaction: tx,
            cancellationToken: cancellationToken));

        var row = await connection.QuerySingleOrDefaultAsync<ClaimRow>(
            new CommandDefinition(
                """
                SELECT status AS Status, transaction_id AS TransactionId,
                       expires_at > (NOW() AT TIME ZONE 'utc') AS Active
                FROM ad_reward_tickets
                WHERE id = @Id AND user_id = @UserId
                FOR UPDATE
                """,
                new { Id = ticketId, UserId = userId },
                transaction: tx,
                cancellationToken: cancellationToken));

        if (row is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return AdRewardClaimResult.NotPending;
        }

        if (row.Status != AdRewardStatuses.Pending)
        {
            await tx.RollbackAsync(cancellationToken);
            return row.TransactionId == transactionId
                ? AdRewardClaimResult.AlreadyClaimedSameTransaction
                : AdRewardClaimResult.NotPending;
        }

        var usedTransaction = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM ad_reward_tickets WHERE transaction_id = @TransactionId)",
            new { TransactionId = transactionId },
            transaction: tx,
            cancellationToken: cancellationToken));
        if (usedTransaction)
        {
            await tx.RollbackAsync(cancellationToken);
            return AdRewardClaimResult.DuplicateTransaction;
        }

        AdRewardClaimResult result;
        if (!row.Active)
        {
            result = AdRewardClaimResult.Expired;
        }
        else
        {
            var rewardedToday = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(*)::int FROM ad_reward_tickets
                WHERE user_id = @UserId AND status = 'REWARDED'
                  AND completed_at >= date_trunc('day', NOW() AT TIME ZONE 'utc')
                """,
                new { UserId = userId },
                transaction: tx,
                cancellationToken: cancellationToken));
            result = rewardedToday >= dailyLimit
                ? AdRewardClaimResult.DailyLimitReached
                : AdRewardClaimResult.Claimed;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE ad_reward_tickets
            SET status = @Status,
                reason = @Reason,
                transaction_id = @TransactionId,
                ad_unit = @AdUnit,
                completed_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id
            """,
            new
            {
                Id = ticketId,
                Status = result == AdRewardClaimResult.Claimed ? AdRewardStatuses.Rewarded : AdRewardStatuses.Rejected,
                Reason = result switch
                {
                    AdRewardClaimResult.Expired => "EXPIRED",
                    AdRewardClaimResult.DailyLimitReached => "DAILY_LIMIT_REACHED",
                    _ => null
                },
                TransactionId = transactionId,
                AdUnit = adUnit
            },
            transaction: tx,
            cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task SetAwardAsync(Guid ticketId, int xpAwarded, string? reason, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE ad_reward_tickets SET xp_awarded = @XpAwarded, reason = @Reason WHERE id = @Id",
            new { Id = ticketId, XpAwarded = xpAwarded, Reason = reason },
            cancellationToken: cancellationToken));
    }

    public async Task RejectAsync(Guid ticketId, string reason, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE ad_reward_tickets
            SET status = 'REJECTED', reason = @Reason, completed_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id AND status = 'PENDING'
            """,
            new { Id = ticketId, Reason = reason },
            cancellationToken: cancellationToken));
    }

    private sealed class ClaimRow
    {
        public string Status { get; init; } = string.Empty;
        public string? TransactionId { get; init; }
        public bool Active { get; init; }
    }

    private sealed class TicketRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public string Token { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int XpAwarded { get; init; }
        public string? Reason { get; init; }
        public string? TransactionId { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }

        public AdRewardTicket ToTicket() =>
            new(Id, UserId, Token, Status, XpAwarded, Reason, TransactionId, ExpiresAt);
    }
}

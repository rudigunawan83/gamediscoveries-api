using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;

namespace GameDiscoveries.Modules.Xp.Data;

public interface IXpStore
{
    Task<XpAwardResult> TryAwardAsync(XpAwardRequest request, int dailyCap, CancellationToken cancellationToken = default);

    Task<UserProgressEntity> GetOrCreateProgressAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<XpTransactionDto>> GetTransactionsAsync(
        Guid userId,
        int limit,
        int offset,
        string? ruleCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<int> GetXpEarnedTodayAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> HasValidSessionBeforeAsync(Guid userId, string? excludeSessionId, CancellationToken cancellationToken = default);

    Task<bool> HasValidSessionForGameAsync(Guid userId, Guid gameId, string? excludeSessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetGameCategoryIdsAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task<bool> HasValidSessionForCategoryAsync(
        Guid userId,
        Guid categoryId,
        string? excludeSessionId,
        CancellationToken cancellationToken = default);

    Task<SessionXpSnapshot?> GetSessionSnapshotAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<XpTransactionEntity?> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<int> CountTransactionsAsync(
        Guid userId,
        string? ruleCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task ApplyLevelAsync(
        Guid userId,
        int level,
        long currentLevelXp,
        CancellationToken cancellationToken = default);

    Task InsertLevelHistoryAsync(
        Guid userId,
        int oldLevel,
        int newLevel,
        long totalXp,
        CancellationToken cancellationToken = default);
}

public sealed record SessionXpSnapshot(
    string SessionId,
    Guid? UserId,
    Guid GameId,
    int ActiveSeconds,
    int DurationSeconds,
    bool IsValid,
    string Status);

public sealed class XpStore(IDbConnectionFactory connectionFactory) : IXpStore
{
    public async Task<XpAwardResult> TryAwardAsync(
        XpAwardRequest request,
        int dailyCap,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var earnedToday = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COALESCE(SUM(xp_amount), 0)::int
                FROM xp_transactions
                WHERE user_id = @UserId
                  AND created_at >= date_trunc('day', NOW() AT TIME ZONE 'utc')
                  AND xp_amount > 0
                """,
                new { request.UserId },
                transaction: tx,
                cancellationToken: cancellationToken));

            var amount = request.XpAmount;

            if (amount > 0 && earnedToday + amount > dailyCap)
            {
                await tx.RollbackAsync(cancellationToken);
                var progress = await GetProgressInternalAsync(connection, request.UserId, tx, cancellationToken);
                return new XpAwardResult(true, 0, progress?.TotalXp ?? 0, "DAILY_XP_CAP_REACHED", []);
            }

            // Negative amounts (reversals/admin) bypass daily cap.
            if (amount < 0)
            {
                var current = await GetProgressInternalAsync(connection, request.UserId, tx, cancellationToken);
                var balance = current?.TotalXp ?? 0;
                if (balance + amount < 0)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new XpAwardResult(false, 0, balance, "INSUFFICIENT_XP", []);
                }
            }

            var transactionId = Guid.NewGuid();
            var id = Guid.NewGuid();
            var metadata = JsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, object?>());

            const string insertSql = """
                INSERT INTO xp_transactions (
                    id, transaction_id, user_id, event_type, reference_type, reference_id,
                    rule_code, xp_amount, description, metadata_json, admin_id,
                    reversal_of_transaction_id, created_at)
                VALUES (
                    @Id, @TransactionId, @UserId, @EventType, @ReferenceType, @ReferenceId,
                    @RuleCode, @XpAmount, @Description, CAST(@MetadataJson AS jsonb), @AdminId,
                    @ReversalOfTransactionId, (NOW() AT TIME ZONE 'utc'))
                ON CONFLICT (user_id, rule_code, reference_type, reference_id) DO NOTHING;
                """;

            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    Id = id,
                    TransactionId = transactionId,
                    request.UserId,
                    request.EventType,
                    request.ReferenceType,
                    request.ReferenceId,
                    request.RuleCode,
                    XpAmount = amount,
                    request.Description,
                    MetadataJson = metadata,
                    request.AdminId,
                    request.ReversalOfTransactionId
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (inserted == 0)
            {
                await tx.CommitAsync(cancellationToken);
                var existingProgress = await GetOrCreateProgressAsync(request.UserId, cancellationToken);
                return new XpAwardResult(true, 0, existingProgress.TotalXp, "ALREADY_REWARDED", []);
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO user_progress (user_id, total_xp, level, current_level_xp, current_streak, longest_streak, created_at, updated_at, last_activity_at)
                VALUES (@UserId, GREATEST(@Amount, 0), 1, 0, 0, 0,
                        (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
                ON CONFLICT (user_id) DO UPDATE SET
                    total_xp = GREATEST(user_progress.total_xp + @Amount, 0),
                    last_activity_at = (NOW() AT TIME ZONE 'utc'),
                    updated_at = (NOW() AT TIME ZONE 'utc');
                """,
                new { request.UserId, Amount = amount },
                transaction: tx,
                cancellationToken: cancellationToken));

            var updated = await GetProgressInternalAsync(connection, request.UserId, tx, cancellationToken);
            var previousLevel = updated?.Level ?? 1;
            var totalXp = updated?.TotalXp ?? Math.Max(amount, 0);

            var levels = (await connection.QueryAsync<LevelDefinitionDto>(new CommandDefinition(
                """
                SELECT level AS Level, required_total_xp AS RequiredTotalXp, title AS Title,
                       description AS Description, is_active AS IsActive
                FROM gamification_levels
                WHERE is_active = TRUE
                ORDER BY required_total_xp
                """,
                transaction: tx,
                cancellationToken: cancellationToken))).ToList();

            var levelInfo = LevelCalculator.Calculate(totalXp, levels);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE user_progress
                SET level = @Level, current_level_xp = @CurrentLevelXp, updated_at = (NOW() AT TIME ZONE 'utc')
                WHERE user_id = @UserId
                """,
                new { request.UserId, levelInfo.Level, levelInfo.CurrentLevelXp },
                transaction: tx,
                cancellationToken: cancellationToken));

            var leveledUp = levelInfo.Level > previousLevel;
            if (leveledUp)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO user_level_history (id, user_id, old_level, new_level, total_xp, created_at)
                    VALUES (@Id, @UserId, @OldLevel, @NewLevel, @TotalXp, (NOW() AT TIME ZONE 'utc'))
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        request.UserId,
                        OldLevel = previousLevel,
                        NewLevel = levelInfo.Level,
                        TotalXp = totalXp
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }

            await tx.CommitAsync(cancellationToken);

            return new XpAwardResult(
                true,
                amount,
                totalXp,
                null,
                [new XpAwardedItem(request.RuleCode, amount)],
                previousLevel,
                levelInfo.Level,
                leveledUp,
                levelInfo);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<UserProgressEntity> GetOrCreateProgressAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_progress (user_id, total_xp, level, current_level_xp, current_streak, longest_streak, created_at, updated_at)
            VALUES (@UserId, 0, 1, 0, 0, 0, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (user_id) DO NOTHING;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        var row = await connection.QuerySingleAsync<UserProgressEntity>(new CommandDefinition(
            """
            SELECT user_id AS UserId, total_xp AS TotalXp, level AS Level,
                   current_level_xp AS CurrentLevelXp, current_streak AS CurrentStreak, longest_streak AS LongestStreak,
                   COALESCE(games_played, 0) AS GamesPlayed,
                   COALESCE(favorites_count, 0) AS FavoritesCount,
                   COALESCE(unique_games_played, 0) AS UniqueGamesPlayed,
                   last_activity_at AS LastActivityAt
            FROM user_progress WHERE user_id = @UserId
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return row;
    }

    public async Task<IReadOnlyList<XpTransactionDto>> GetTransactionsAsync(
        Guid userId,
        int limit,
        int offset,
        string? ruleCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<XpTransactionDto>(new CommandDefinition(
            """
            SELECT
                transaction_id AS TransactionId,
                rule_code AS RuleCode,
                event_type AS EventType,
                reference_type AS ReferenceType,
                reference_id AS ReferenceId,
                xp_amount AS XpAmount,
                description AS Description,
                created_at AS CreatedAt
            FROM xp_transactions
            WHERE user_id = @UserId
              AND (@RuleCode IS NULL OR rule_code = @RuleCode)
              AND (@From::timestamptz IS NULL OR created_at >= @From)
              AND (@To::timestamptz IS NULL OR created_at <= @To)
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset
            """,
            new
            {
                UserId = userId,
                RuleCode = ruleCode,
                From = from?.UtcDateTime,
                To = to?.UtcDateTime,
                Limit = Math.Clamp(limit, 1, 100),
                Offset = Math.Max(0, offset)
            },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> GetXpEarnedTodayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(xp_amount), 0)::int
            FROM xp_transactions
            WHERE user_id = @UserId
              AND created_at >= date_trunc('day', NOW() AT TIME ZONE 'utc')
              AND xp_amount > 0
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> HasValidSessionBeforeAsync(
        Guid userId,
        string? excludeSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS(
                SELECT 1 FROM game_play_sessions
                WHERE user_id = @UserId
                  AND is_valid = TRUE
                  AND (@ExcludeSessionId IS NULL OR session_id <> @ExcludeSessionId)
            )
            """,
            new { UserId = userId, ExcludeSessionId = excludeSessionId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> HasValidSessionForGameAsync(
        Guid userId,
        Guid gameId,
        string? excludeSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS(
                SELECT 1 FROM game_play_sessions
                WHERE user_id = @UserId
                  AND game_id = @GameId
                  AND is_valid = TRUE
                  AND (@ExcludeSessionId IS NULL OR session_id <> @ExcludeSessionId)
            )
            """,
            new { UserId = userId, GameId = gameId, ExcludeSessionId = excludeSessionId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Guid>> GetGameCategoryIdsAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT category_id FROM game_categories WHERE game_id = @GameId",
            new { GameId = gameId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<bool> HasValidSessionForCategoryAsync(
        Guid userId,
        Guid categoryId,
        string? excludeSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS(
                SELECT 1
                FROM game_play_sessions s
                INNER JOIN game_categories gc ON gc.game_id = s.game_id
                WHERE s.user_id = @UserId
                  AND gc.category_id = @CategoryId
                  AND s.is_valid = TRUE
                  AND (@ExcludeSessionId IS NULL OR s.session_id <> @ExcludeSessionId)
            )
            """,
            new { UserId = userId, CategoryId = categoryId, ExcludeSessionId = excludeSessionId },
            cancellationToken: cancellationToken));
    }

    public async Task<SessionXpSnapshot?> GetSessionSnapshotAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<SessionXpSnapshot>(new CommandDefinition(
            """
            SELECT
                session_id AS SessionId,
                user_id AS UserId,
                game_id AS GameId,
                active_seconds AS ActiveSeconds,
                duration_seconds AS DurationSeconds,
                is_valid AS IsValid,
                status AS Status
            FROM game_play_sessions
            WHERE session_id = @SessionId
            """,
            new { SessionId = sessionId },
            cancellationToken: cancellationToken));
    }

    public async Task<XpTransactionEntity?> GetTransactionByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<XpTransactionEntity>(new CommandDefinition(
            """
            SELECT
                id AS Id,
                transaction_id AS TransactionId,
                user_id AS UserId,
                event_type AS EventType,
                reference_type AS ReferenceType,
                reference_id AS ReferenceId,
                rule_code AS RuleCode,
                xp_amount AS XpAmount,
                description AS Description,
                metadata_json::text AS MetadataJson,
                admin_id AS AdminId,
                reversal_of_transaction_id AS ReversalOfTransactionId,
                created_at AS CreatedAt
            FROM xp_transactions
            WHERE transaction_id = @TransactionId
            """,
            new { TransactionId = transactionId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> CountTransactionsAsync(
        Guid userId,
        string? ruleCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM xp_transactions
            WHERE user_id = @UserId
              AND (@RuleCode IS NULL OR rule_code = @RuleCode)
              AND (@From::timestamptz IS NULL OR created_at >= @From)
              AND (@To::timestamptz IS NULL OR created_at <= @To)
            """,
            new
            {
                UserId = userId,
                RuleCode = ruleCode,
                From = from?.UtcDateTime,
                To = to?.UtcDateTime
            },
            cancellationToken: cancellationToken));
    }

    public async Task ApplyLevelAsync(
        Guid userId,
        int level,
        long currentLevelXp,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_progress
            SET level = @Level, current_level_xp = @CurrentLevelXp, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE user_id = @UserId
            """,
            new { UserId = userId, Level = level, CurrentLevelXp = currentLevelXp },
            cancellationToken: cancellationToken));
    }

    public async Task InsertLevelHistoryAsync(
        Guid userId,
        int oldLevel,
        int newLevel,
        long totalXp,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_level_history (id, user_id, old_level, new_level, total_xp, created_at)
            VALUES (@Id, @UserId, @OldLevel, @NewLevel, @TotalXp, (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OldLevel = oldLevel,
                NewLevel = newLevel,
                TotalXp = totalXp
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<UserProgressEntity?> GetProgressInternalAsync(
        DbConnection connection,
        Guid userId,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<UserProgressEntity>(new CommandDefinition(
            """
            SELECT user_id AS UserId, total_xp AS TotalXp, level AS Level,
                   current_level_xp AS CurrentLevelXp, current_streak AS CurrentStreak, longest_streak AS LongestStreak,
                   COALESCE(games_played, 0) AS GamesPlayed,
                   COALESCE(favorites_count, 0) AS FavoritesCount,
                   COALESCE(unique_games_played, 0) AS UniqueGamesPlayed,
                   last_activity_at AS LastActivityAt
            FROM user_progress WHERE user_id = @UserId
            """,
            new { UserId = userId },
            transaction: tx,
            cancellationToken: cancellationToken));
    }
}

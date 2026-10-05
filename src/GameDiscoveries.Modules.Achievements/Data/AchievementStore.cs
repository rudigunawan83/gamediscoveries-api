using System.Data.Common;
using System.Text.Json;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Achievements.Models;

namespace GameDiscoveries.Modules.Achievements.Data;

public interface IAchievementStore
{
    Task<IReadOnlyList<AchievementDefinitionEntity>> GetDefinitionsAsync(
        bool activeOnly,
        IReadOnlySet<string>? requirementTypes,
        CancellationToken cancellationToken = default);

    Task<AchievementDefinitionEntity?> GetDefinitionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AchievementDefinitionEntity?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<AchievementDefinitionEntity> CreateDefinitionAsync(
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default);

    Task<AchievementDefinitionEntity> UpdateDefinitionAsync(
        Guid id,
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, AchievementUnlockEntity>> GetUnlocksAsync(
        Guid userId,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default);

    Task<AchievementUnlockEntity?> GetUnlockAsync(
        Guid userId,
        Guid definitionId,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default);

    Task<AchievementUnlockEntity?> TryInsertUnlockAsync(
        Guid userId,
        AchievementDefinitionEntity definition,
        int progressValue,
        Guid? grantedByAdminId,
        CancellationToken cancellationToken = default);

    Task SetRewardTransactionAsync(Guid unlockId, Guid rewardTransactionId, CancellationToken cancellationToken = default);

    Task<bool> RevokeUnlockAsync(
        Guid userId,
        Guid definitionId,
        Guid adminId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task InsertHistoryAsync(
        Guid userId,
        Guid definitionId,
        string eventType,
        Guid? adminId,
        string? reason,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementHistoryDto>> GetRecentHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminAchievementUserDto>> GetUsersForDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task<AdminAchievementOverviewStats> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetMetricsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AchievementStore(IDbConnectionFactory connectionFactory) : IAchievementStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<AchievementDefinitionEntity>> GetDefinitionsAsync(
        bool activeOnly,
        IReadOnlySet<string>? requirementTypes,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AchievementDefinitionEntity>(new CommandDefinition(
            """
            SELECT d.id AS Id, d.code AS Code, d.title AS Title, d.description AS Description,
                   d.category AS Category, d.difficulty AS Difficulty, d.requirement_type AS RequirementType,
                   d.target_value AS TargetValue, d.reward_xp AS RewardXp, d.icon AS Icon,
                   d.is_secret AS IsSecret, d.is_active AS IsActive, d.season_id AS SeasonId,
                   d.created_at AS CreatedAt, d.updated_at AS UpdatedAt,
                   COUNT(u.id) FILTER (WHERE u.revoked_at IS NULL)::bigint AS UnlockedCount
            FROM achievement_definitions d
            LEFT JOIN user_achievement_unlocks u ON u.achievement_definition_id = d.id
            WHERE (@ActiveOnly = FALSE OR d.is_active = TRUE)
              AND (@RequirementCount = 0 OR d.requirement_type = ANY(@RequirementTypes))
            GROUP BY d.id
            ORDER BY d.sort_order, d.created_at
            """,
            new
            {
                ActiveOnly = activeOnly,
                RequirementCount = requirementTypes?.Count ?? 0,
                RequirementTypes = requirementTypes?.ToArray() ?? []
            },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public Task<AchievementDefinitionEntity?> GetDefinitionByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetDefinitionAsync("d.id = @Id", new { Id = id }, cancellationToken);

    public Task<AchievementDefinitionEntity?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        GetDefinitionAsync("LOWER(d.code) = LOWER(@Code)", new { Code = code }, cancellationToken);

    public async Task<AchievementDefinitionEntity> CreateDefinitionAsync(
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO achievement_definitions (
                id, code, title, description, category, difficulty, requirement_type,
                target_value, reward_xp, icon, is_secret, is_active, season_id, created_at, updated_at)
            VALUES (
                @Id, @Code, @Title, @Description, @Category, @Difficulty, @RequirementType,
                @TargetValue, @RewardXp, @Icon, @IsSecret, @IsActive, @SeasonId,
                (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = id,
                Code = request.Code.Trim().ToUpperInvariant(),
                request.Title,
                request.Description,
                request.Category,
                request.Difficulty,
                request.RequirementType,
                request.TargetValue,
                request.RewardXp,
                request.Icon,
                request.IsSecret,
                request.IsActive,
                request.SeasonId
            },
            cancellationToken: cancellationToken));
        return (await GetDefinitionByIdAsync(id, cancellationToken))!;
    }

    public async Task<AchievementDefinitionEntity> UpdateDefinitionAsync(
        Guid id,
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE achievement_definitions SET
                code = @Code,
                title = @Title,
                description = @Description,
                category = @Category,
                difficulty = @Difficulty,
                requirement_type = @RequirementType,
                target_value = @TargetValue,
                reward_xp = @RewardXp,
                icon = @Icon,
                is_secret = @IsSecret,
                is_active = @IsActive,
                season_id = @SeasonId,
                updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id
            """,
            new
            {
                Id = id,
                Code = request.Code.Trim().ToUpperInvariant(),
                request.Title,
                request.Description,
                request.Category,
                request.Difficulty,
                request.RequirementType,
                request.TargetValue,
                request.RewardXp,
                request.Icon,
                request.IsSecret,
                request.IsActive,
                request.SeasonId
            },
            cancellationToken: cancellationToken));
        return (await GetDefinitionByIdAsync(id, cancellationToken))!;
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE achievement_definitions
            SET is_active = @IsActive, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id
            """,
            new { Id = id, IsActive = isActive },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<Guid, AchievementUnlockEntity>> GetUnlocksAsync(
        Guid userId,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AchievementUnlockEntity>(new CommandDefinition(
            """
            SELECT id AS Id, user_id AS UserId, achievement_definition_id AS AchievementDefinitionId,
                   unlocked_at AS UnlockedAt, progress_value AS ProgressValue, target_value AS TargetValue,
                   reward_transaction_id AS RewardTransactionId, is_notified AS IsNotified,
                   granted_by_admin_id AS GrantedByAdminId, revoked_at AS RevokedAt,
                   revoked_by_admin_id AS RevokedByAdminId, revoke_reason AS RevokeReason
            FROM user_achievement_unlocks
            WHERE user_id = @UserId AND (@IncludeRevoked = TRUE OR revoked_at IS NULL)
            """,
            new { UserId = userId, IncludeRevoked = includeRevoked },
            cancellationToken: cancellationToken));
        return rows.ToDictionary(x => x.AchievementDefinitionId);
    }

    public async Task<AchievementUnlockEntity?> GetUnlockAsync(
        Guid userId,
        Guid definitionId,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AchievementUnlockEntity>(new CommandDefinition(
            """
            SELECT id AS Id, user_id AS UserId, achievement_definition_id AS AchievementDefinitionId,
                   unlocked_at AS UnlockedAt, progress_value AS ProgressValue, target_value AS TargetValue,
                   reward_transaction_id AS RewardTransactionId, is_notified AS IsNotified,
                   granted_by_admin_id AS GrantedByAdminId, revoked_at AS RevokedAt,
                   revoked_by_admin_id AS RevokedByAdminId, revoke_reason AS RevokeReason
            FROM user_achievement_unlocks
            WHERE user_id = @UserId
              AND achievement_definition_id = @DefinitionId
              AND (@IncludeRevoked = TRUE OR revoked_at IS NULL)
            """,
            new { UserId = userId, DefinitionId = definitionId, IncludeRevoked = includeRevoked },
            cancellationToken: cancellationToken));
    }

    public async Task<AchievementUnlockEntity?> TryInsertUnlockAsync(
        Guid userId,
        AchievementDefinitionEntity definition,
        int progressValue,
        Guid? grantedByAdminId,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<AchievementUnlockEntity>(new CommandDefinition(
                """
                SELECT id AS Id, user_id AS UserId, achievement_definition_id AS AchievementDefinitionId,
                       unlocked_at AS UnlockedAt, progress_value AS ProgressValue, target_value AS TargetValue,
                       reward_transaction_id AS RewardTransactionId, is_notified AS IsNotified,
                       granted_by_admin_id AS GrantedByAdminId, revoked_at AS RevokedAt,
                       revoked_by_admin_id AS RevokedByAdminId, revoke_reason AS RevokeReason
                FROM user_achievement_unlocks
                WHERE user_id = @UserId AND achievement_definition_id = @DefinitionId
                FOR UPDATE
                """,
                new { UserId = userId, DefinitionId = definition.Id },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (existing is { RevokedAt: null })
            {
                await tx.CommitAsync(cancellationToken);
                return null;
            }

            if (existing is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE user_achievement_unlocks
                    SET unlocked_at = (NOW() AT TIME ZONE 'utc'),
                        progress_value = @ProgressValue,
                        target_value = @TargetValue,
                        granted_by_admin_id = @GrantedByAdminId,
                        revoked_at = NULL,
                        revoked_by_admin_id = NULL,
                        revoke_reason = NULL,
                        updated_at = (NOW() AT TIME ZONE 'utc')
                    WHERE id = @Id
                    """,
                    new
                    {
                        existing.Id,
                        ProgressValue = progressValue,
                        TargetValue = definition.TargetValue,
                        GrantedByAdminId = grantedByAdminId
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
                id = existing.Id;
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO user_achievement_unlocks (
                        id, user_id, achievement_definition_id, unlocked_at, progress_value, target_value,
                        granted_by_admin_id, created_at, updated_at)
                    VALUES (
                        @Id, @UserId, @DefinitionId, (NOW() AT TIME ZONE 'utc'), @ProgressValue, @TargetValue,
                        @GrantedByAdminId, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
                    ON CONFLICT (user_id, achievement_definition_id) DO NOTHING
                    """,
                    new
                    {
                        Id = id,
                        UserId = userId,
                        DefinitionId = definition.Id,
                        ProgressValue = progressValue,
                        TargetValue = definition.TargetValue,
                        GrantedByAdminId = grantedByAdminId
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }

            await tx.CommitAsync(cancellationToken);
            return await GetUnlockAsync(userId, definition.Id, false, cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SetRewardTransactionAsync(Guid unlockId, Guid rewardTransactionId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_achievement_unlocks
            SET reward_transaction_id = @RewardTransactionId, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @UnlockId
            """,
            new { UnlockId = unlockId, RewardTransactionId = rewardTransactionId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RevokeUnlockAsync(
        Guid userId,
        Guid definitionId,
        Guid adminId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_achievement_unlocks
            SET revoked_at = (NOW() AT TIME ZONE 'utc'),
                revoked_by_admin_id = @AdminId,
                revoke_reason = @Reason,
                updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE user_id = @UserId
              AND achievement_definition_id = @DefinitionId
              AND revoked_at IS NULL
            """,
            new { UserId = userId, DefinitionId = definitionId, AdminId = adminId, Reason = reason },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task InsertHistoryAsync(
        Guid userId,
        Guid definitionId,
        string eventType,
        Guid? adminId,
        string? reason,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO achievement_history (
                id, user_id, achievement_definition_id, event_type, admin_id, reason, metadata_json, created_at)
            VALUES (
                @Id, @UserId, @DefinitionId, @EventType, @AdminId, @Reason,
                CAST(@Metadata AS jsonb), (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DefinitionId = definitionId,
                EventType = eventType,
                AdminId = adminId,
                Reason = reason,
                Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions)
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AchievementHistoryDto>> GetRecentHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AchievementHistoryDto>(new CommandDefinition(
            """
            SELECT id AS Id, user_id AS UserId, achievement_definition_id AS AchievementDefinitionId,
                   event_type AS EventType, reason AS Reason, created_at AS CreatedAt
            FROM achievement_history
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            LIMIT @Limit
            """,
            new { UserId = userId, Limit = Math.Clamp(limit, 1, 100) },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AdminAchievementUserDto>> GetUsersForDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AdminAchievementUserDto>(new CommandDefinition(
            """
            SELECT user_id AS UserId, unlocked_at AS UnlockedAt, progress_value AS ProgressValue,
                   target_value AS TargetValue, granted_by_admin_id AS GrantedByAdminId, revoked_at AS RevokedAt
            FROM user_achievement_unlocks
            WHERE achievement_definition_id = @DefinitionId
            ORDER BY unlocked_at DESC
            LIMIT 500
            """,
            new { DefinitionId = definitionId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<AdminAchievementOverviewStats> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*) FROM achievement_definitions)::bigint AS TotalDefinitions,
                (SELECT COUNT(*) FROM achievement_definitions WHERE is_active = TRUE)::bigint AS ActiveDefinitions,
                (SELECT COUNT(*) FROM achievement_definitions WHERE is_secret = TRUE)::bigint AS SecretDefinitions,
                (SELECT COUNT(*) FROM user_achievement_unlocks WHERE revoked_at IS NULL)::bigint AS TotalUnlocks,
                (SELECT COUNT(*) FROM user_achievement_unlocks
                 WHERE revoked_at IS NULL AND unlocked_at >= (NOW() AT TIME ZONE 'utc') - INTERVAL '1 day')::bigint AS UnlocksToday
            """,
            cancellationToken: cancellationToken));
        return new AdminAchievementOverviewStats(
            (long)row.totaldefinitions,
            (long)row.activedefinitions,
            (long)row.secretdefinitions,
            (long)row.totalunlocks,
            (long)row.unlockstoday);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetMetricsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*)::int FROM game_play_sessions
                 WHERE user_id = @UserId AND is_valid = TRUE) AS ValidSessionsCount,
                (SELECT COUNT(DISTINCT game_id)::int FROM game_play_sessions
                 WHERE user_id = @UserId AND is_valid = TRUE) AS UniqueGamesPlayed,
                (SELECT COUNT(DISTINCT gc.category_id)::int
                 FROM game_play_sessions s
                 INNER JOIN game_categories gc ON gc.game_id = s.game_id
                 WHERE s.user_id = @UserId AND s.is_valid = TRUE) AS UniqueGenresPlayed,
                (SELECT COUNT(*)::int FROM user_favorites WHERE user_id = @UserId) AS FavoritesCount,
                (SELECT COUNT(*)::int FROM game_reviews
                 WHERE user_id = @UserId AND rating > 0 AND status = 'published' AND deleted_at IS NULL) AS RatingsCount,
                (SELECT COUNT(*)::int FROM game_reviews
                 WHERE user_id = @UserId AND rating > 0 AND status = 'published'
                   AND deleted_at IS NULL AND NULLIF(BTRIM(content), '') IS NOT NULL) AS ReviewsCount,
                COALESCE((SELECT SUM(active_seconds)::int FROM game_play_sessions
                 WHERE user_id = @UserId AND is_valid = TRUE), 0)::int AS TotalActiveTime,
                COALESCE((SELECT level FROM user_progress WHERE user_id = @UserId), 1)::int AS CurrentLevel,
                COALESCE((SELECT total_xp FROM user_progress WHERE user_id = @UserId), 0)::int AS TotalXp,
                COALESCE((SELECT longest_streak FROM user_progress WHERE user_id = @UserId), 0)::int AS LongestStreak,
                (SELECT COUNT(*)::int FROM user_missions
                 WHERE user_id = @UserId AND type = 'DAILY' AND status = 'COMPLETED') AS DailyMissionsCompleted,
                (SELECT COUNT(*)::int FROM user_missions
                 WHERE user_id = @UserId AND type = 'WEEKLY' AND status = 'COMPLETED') AS WeeklyChallengesCompleted,
                (SELECT COUNT(*)::int FROM user_achievement_unlocks
                 WHERE user_id = @UserId AND revoked_at IS NULL) AS AchievementCount
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["FIRST_GAME_PLAYED"] = (int)row.validsessionscount >= 1 ? 1 : 0,
            ["GAMES_PLAYED"] = (int)row.validsessionscount,
            ["UNIQUE_GAMES_PLAYED"] = (int)row.uniquegamesplayed,
            ["GAMES_DISCOVERED"] = (int)row.uniquegamesplayed,
            ["UNIQUE_GENRES_PLAYED"] = (int)row.uniquegenresplayed,
            ["FAVORITES_COUNT"] = (int)row.favoritescount,
            ["RATINGS_COUNT"] = (int)row.ratingscount,
            ["REVIEWS_COUNT"] = (int)row.reviewscount,
            ["TOTAL_ACTIVE_TIME"] = (int)row.totalactivetime,
            ["VALID_SESSIONS_COUNT"] = (int)row.validsessionscount,
            ["STREAK_DAYS"] = (int)row.longeststreak,
            ["LONGEST_STREAK"] = (int)row.longeststreak,
            ["CURRENT_LEVEL"] = (int)row.currentlevel,
            ["TOTAL_XP"] = (int)row.totalxp,
            ["DAILY_MISSIONS_COMPLETED"] = (int)row.dailymissionscompleted,
            ["WEEKLY_CHALLENGES_COMPLETED"] = (int)row.weeklychallengescompleted,
            ["ACHIEVEMENT_COUNT"] = (int)row.achievementcount,
            ["SPECIAL_CONDITION"] = 0
        };
    }

    private async Task<AchievementDefinitionEntity?> GetDefinitionAsync(
        string predicate,
        object args,
        CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var sql = $"""
            SELECT d.id AS Id, d.code AS Code, d.title AS Title, d.description AS Description,
                   d.category AS Category, d.difficulty AS Difficulty, d.requirement_type AS RequirementType,
                   d.target_value AS TargetValue, d.reward_xp AS RewardXp, d.icon AS Icon,
                   d.is_secret AS IsSecret, d.is_active AS IsActive, d.season_id AS SeasonId,
                   d.created_at AS CreatedAt, d.updated_at AS UpdatedAt,
                   COUNT(u.id) FILTER (WHERE u.revoked_at IS NULL)::bigint AS UnlockedCount
            FROM achievement_definitions d
            LEFT JOIN user_achievement_unlocks u ON u.achievement_definition_id = d.id
            WHERE {predicate}
            GROUP BY d.id
            """;
        return await connection.QuerySingleOrDefaultAsync<AchievementDefinitionEntity>(
            new CommandDefinition(sql, args, cancellationToken: cancellationToken));
    }
}

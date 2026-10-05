using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Missions.Domain;
using GameDiscoveries.Modules.Missions.Models;

namespace GameDiscoveries.Modules.Missions.Data;

public interface IMissionStore
{
    Task<IReadOnlyList<MissionTemplateEntity>> GetActiveTemplatesAsync(
        string? type,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MissionTemplateEntity>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);

    Task<MissionTemplateEntity?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MissionTemplateEntity?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task UpsertTemplateAsync(MissionTemplateEntity entity, CancellationToken cancellationToken = default);

    Task SetTemplateActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMissionEntity>> GetUserMissionsForPeriodAsync(
        Guid userId,
        string type,
        DateTimeOffset periodStart,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMissionEntity>> GetActiveUserMissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserMissionEntity?> GetUserMissionAsync(Guid userId, Guid missionId, CancellationToken cancellationToken = default);

    Task InsertUserMissionAsync(UserMissionEntity mission, CancellationToken cancellationToken = default);

    Task ExpirePastMissionsAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    Task ExpireAllPastMissionsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    Task<int> ComputeProgressAsync(
        Guid userId,
        string requirementType,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        string timeZoneId,
        CancellationToken cancellationToken = default);

    Task<(UserMissionEntity Mission, bool JustCompleted)> UpdateProgressAndCompleteAsync(
        Guid missionId,
        Guid userId,
        int progressValue,
        CancellationToken cancellationToken = default);

    Task MarkRewardTransactionAsync(
        Guid missionId,
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<UserMissionEntity> Items, int Total)> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? type,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<long> GetUserTotalXpAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<MissionAnalyticsResponse> GetAnalyticsAsync(CancellationToken cancellationToken = default);
}

public sealed class MissionStore(IDbConnectionFactory connectionFactory) : IMissionStore
{
    public async Task<IReadOnlyList<MissionTemplateEntity>> GetActiveTemplatesAsync(
        string? type,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MissionTemplateEntity>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, type AS Type, title AS Title, description AS Description,
                   icon AS Icon, requirement_type AS RequirementType, target_value AS TargetValue,
                   reward_xp AS RewardXp, difficulty AS Difficulty, is_active AS IsActive,
                   sort_order AS SortOrder, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM mission_templates
            WHERE is_active = TRUE
              AND (@Type IS NULL OR type = @Type)
            ORDER BY sort_order, code
            """,
            new { Type = type },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MissionTemplateEntity>> GetAllTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MissionTemplateEntity>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, type AS Type, title AS Title, description AS Description,
                   icon AS Icon, requirement_type AS RequirementType, target_value AS TargetValue,
                   reward_xp AS RewardXp, difficulty AS Difficulty, is_active AS IsActive,
                   sort_order AS SortOrder, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM mission_templates
            ORDER BY type, sort_order, code
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<MissionTemplateEntity?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<MissionTemplateEntity>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, type AS Type, title AS Title, description AS Description,
                   icon AS Icon, requirement_type AS RequirementType, target_value AS TargetValue,
                   reward_xp AS RewardXp, difficulty AS Difficulty, is_active AS IsActive,
                   sort_order AS SortOrder, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM mission_templates WHERE id = @Id
            """,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public async Task<MissionTemplateEntity?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<MissionTemplateEntity>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, type AS Type, title AS Title, description AS Description,
                   icon AS Icon, requirement_type AS RequirementType, target_value AS TargetValue,
                   reward_xp AS RewardXp, difficulty AS Difficulty, is_active AS IsActive,
                   sort_order AS SortOrder, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM mission_templates WHERE code = @Code
            """,
            new { Code = code },
            cancellationToken: cancellationToken));
    }

    public async Task UpsertTemplateAsync(MissionTemplateEntity entity, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO mission_templates (
                id, code, type, title, description, icon, requirement_type, target_value,
                reward_xp, difficulty, is_active, sort_order, created_at, updated_at)
            VALUES (
                @Id, @Code, @Type, @Title, @Description, @Icon, @RequirementType, @TargetValue,
                @RewardXp, @Difficulty, @IsActive, @SortOrder,
                (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (code) DO UPDATE SET
                type = EXCLUDED.type,
                title = EXCLUDED.title,
                description = EXCLUDED.description,
                icon = EXCLUDED.icon,
                requirement_type = EXCLUDED.requirement_type,
                target_value = EXCLUDED.target_value,
                reward_xp = EXCLUDED.reward_xp,
                difficulty = EXCLUDED.difficulty,
                is_active = EXCLUDED.is_active,
                sort_order = EXCLUDED.sort_order,
                updated_at = (NOW() AT TIME ZONE 'utc')
            """,
            entity,
            cancellationToken: cancellationToken));
    }

    public async Task SetTemplateActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mission_templates
            SET is_active = @IsActive, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id
            """,
            new { Id = id, IsActive = isActive },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<UserMissionEntity>> GetUserMissionsForPeriodAsync(
        Guid userId,
        string type,
        DateTimeOffset periodStart,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserMissionEntity>(new CommandDefinition(
            UserMissionSelect + """
            WHERE user_id = @UserId AND type = @Type AND period_start = @PeriodStart
            ORDER BY created_at
            """,
            new { UserId = userId, Type = type, PeriodStart = periodStart.UtcDateTime },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<UserMissionEntity>> GetActiveUserMissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserMissionEntity>(new CommandDefinition(
            UserMissionSelect + """
            WHERE user_id = @UserId AND status = 'ACTIVE'
            ORDER BY type, created_at
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<UserMissionEntity?> GetUserMissionAsync(
        Guid userId,
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserMissionEntity>(new CommandDefinition(
            UserMissionSelect + " WHERE id = @Id AND user_id = @UserId",
            new { Id = missionId, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task InsertUserMissionAsync(UserMissionEntity mission, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_missions (
                id, user_id, mission_template_id, code, type, title, description, requirement_type,
                period_start, period_end, progress_value, target_value, reward_xp, status,
                completed_at, reward_transaction_id, created_at, updated_at)
            VALUES (
                @Id, @UserId, @MissionTemplateId, @Code, @Type, @Title, @Description, @RequirementType,
                @PeriodStart, @PeriodEnd, @ProgressValue, @TargetValue, @RewardXp, @Status,
                @CompletedAt, @RewardTransactionId,
                (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (user_id, mission_template_id, period_start) DO NOTHING
            """,
            new
            {
                mission.Id,
                mission.UserId,
                mission.MissionTemplateId,
                mission.Code,
                mission.Type,
                mission.Title,
                mission.Description,
                mission.RequirementType,
                PeriodStart = mission.PeriodStart.UtcDateTime,
                PeriodEnd = mission.PeriodEnd.UtcDateTime,
                mission.ProgressValue,
                mission.TargetValue,
                mission.RewardXp,
                mission.Status,
                CompletedAt = mission.CompletedAt?.UtcDateTime,
                mission.RewardTransactionId
            },
            cancellationToken: cancellationToken));
    }

    public async Task ExpirePastMissionsAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_missions
            SET status = 'EXPIRED', updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE user_id = @UserId
              AND status = 'ACTIVE'
              AND period_end < @Now
            """,
            new { UserId = userId, Now = utcNow.UtcDateTime },
            cancellationToken: cancellationToken));
    }

    public async Task ExpireAllPastMissionsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_missions
            SET status = 'EXPIRED', updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE status = 'ACTIVE' AND period_end < @Now
            """,
            new { Now = utcNow.UtcDateTime },
            cancellationToken: cancellationToken));
    }

    public async Task<int> ComputeProgressAsync(
        Guid userId,
        string requirementType,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var start = periodStart.UtcDateTime;
        var end = periodEnd.UtcDateTime;

        return requirementType.ToUpperInvariant() switch
        {
            MissionRequirementTypes.UniqueGamesPlayed =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(DISTINCT game_id)::int
                    FROM game_play_sessions
                    WHERE user_id = @UserId AND is_valid = TRUE
                      AND COALESCE(ended_at, updated_at, created_at) >= @Start
                      AND COALESCE(ended_at, updated_at, created_at) <= @End
                    """,
                    new { UserId = userId, Start = start, End = end },
                    cancellationToken: cancellationToken)),

            MissionRequirementTypes.NewGameDiscovered =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(DISTINCT s.game_id)::int
                    FROM game_play_sessions s
                    WHERE s.user_id = @UserId AND s.is_valid = TRUE
                      AND COALESCE(s.ended_at, s.updated_at, s.created_at) >= @Start
                      AND COALESCE(s.ended_at, s.updated_at, s.created_at) <= @End
                      AND NOT EXISTS (
                          SELECT 1 FROM game_play_sessions prior
                          WHERE prior.user_id = s.user_id
                            AND prior.game_id = s.game_id
                            AND prior.is_valid = TRUE
                            AND COALESCE(prior.ended_at, prior.updated_at, prior.created_at) < @Start
                      )
                    """,
                    new { UserId = userId, Start = start, End = end },
                    cancellationToken: cancellationToken)),

            MissionRequirementTypes.NewGenreDiscovered =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(DISTINCT gc.category_id)::int
                    FROM game_play_sessions s
                    INNER JOIN game_categories gc ON gc.game_id = s.game_id
                    WHERE s.user_id = @UserId AND s.is_valid = TRUE
                      AND COALESCE(s.ended_at, s.updated_at, s.created_at) >= @Start
                      AND COALESCE(s.ended_at, s.updated_at, s.created_at) <= @End
                      AND NOT EXISTS (
                          SELECT 1
                          FROM game_play_sessions prior
                          INNER JOIN game_categories pgc ON pgc.game_id = prior.game_id
                          WHERE prior.user_id = s.user_id
                            AND pgc.category_id = gc.category_id
                            AND prior.is_valid = TRUE
                            AND COALESCE(prior.ended_at, prior.updated_at, prior.created_at) < @Start
                      )
                    """,
                    new { UserId = userId, Start = start, End = end },
                    cancellationToken: cancellationToken)),

            MissionRequirementTypes.ActiveTimeSeconds =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COALESCE(SUM(active_seconds), 0)::int
                    FROM game_play_sessions
                    WHERE user_id = @UserId AND is_valid = TRUE
                      AND COALESCE(ended_at, updated_at, created_at) >= @Start
                      AND COALESCE(ended_at, updated_at, created_at) <= @End
                    """,
                    new { UserId = userId, Start = start, End = end },
                    cancellationToken: cancellationToken)),

            MissionRequirementTypes.FavoritesAdded =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(*)::int
                    FROM user_favorites
                    WHERE user_id = @UserId
                      AND created_at >= @Start AND created_at <= @End
                    """,
                    new { UserId = userId, Start = start, End = end },
                    cancellationToken: cancellationToken)),

            MissionRequirementTypes.ActiveDays =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT CASE
                        WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'user_activity_days')
                        THEN (
                            SELECT COUNT(*)::int FROM user_activity_days
                            WHERE user_id = @UserId
                              AND activity_date >= ((@Start AT TIME ZONE 'utc') AT TIME ZONE @Tz)::date
                              AND activity_date <= ((@End AT TIME ZONE 'utc') AT TIME ZONE @Tz)::date
                        )
                        ELSE (
                            SELECT COUNT(DISTINCT ((COALESCE(ended_at, updated_at, created_at) AT TIME ZONE 'utc')
                                AT TIME ZONE @Tz)::date)::int
                            FROM game_play_sessions
                            WHERE user_id = @UserId AND is_valid = TRUE
                              AND COALESCE(ended_at, updated_at, created_at) >= @Start
                              AND COALESCE(ended_at, updated_at, created_at) <= @End
                        )
                    END
                    """,
                    new { UserId = userId, Start = start, End = end, Tz = timeZoneId },
                    cancellationToken: cancellationToken)),

            MissionRequirementTypes.UniqueGenresPlayed =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(DISTINCT gc.category_id)::int
                    FROM game_play_sessions s
                    INNER JOIN game_categories gc ON gc.game_id = s.game_id
                    WHERE s.user_id = @UserId AND s.is_valid = TRUE
                      AND COALESCE(s.ended_at, s.updated_at, s.created_at) >= @Start
                      AND COALESCE(s.ended_at, s.updated_at, s.created_at) <= @End
                    """,
                    new { UserId = userId, Start = start, End = end },
                    cancellationToken: cancellationToken)),

            MissionRequirementTypes.TotalValidSessions =>
                await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(*)::int
                    FROM game_play_sessions
                    WHERE user_id = @UserId AND is_valid = TRUE
                      AND COALESCE(ended_at, updated_at, created_at) >= @Start
                      AND COALESCE(ended_at, updated_at, created_at) <= @End
                    """,
                    new { UserId = userId, Start = start, End = end },
                    cancellationToken: cancellationToken)),

            _ => 0
        };
    }

    public async Task<(UserMissionEntity Mission, bool JustCompleted)> UpdateProgressAndCompleteAsync(
        Guid missionId,
        Guid userId,
        int progressValue,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var mission = await connection.QuerySingleOrDefaultAsync<UserMissionEntity>(new CommandDefinition(
                UserMissionSelect + " WHERE id = @Id AND user_id = @UserId FOR UPDATE",
                new { Id = missionId, UserId = userId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (mission is null)
            {
                throw new InvalidOperationException("Mission not found.");
            }

            if (!string.Equals(mission.Status, MissionStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                await tx.CommitAsync(cancellationToken);
                return (mission, false);
            }

            var clamped = Math.Min(progressValue, mission.TargetValue);
            var justCompleted = clamped >= mission.TargetValue;
            if (justCompleted)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE user_missions
                    SET progress_value = @Progress,
                        status = 'COMPLETED',
                        completed_at = (NOW() AT TIME ZONE 'utc'),
                        updated_at = (NOW() AT TIME ZONE 'utc')
                    WHERE id = @Id AND status = 'ACTIVE'
                    """,
                    new { Id = missionId, Progress = clamped },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE user_missions
                    SET progress_value = @Progress, updated_at = (NOW() AT TIME ZONE 'utc')
                    WHERE id = @Id AND status = 'ACTIVE'
                    """,
                    new { Id = missionId, Progress = clamped },
                    transaction: tx,
                    cancellationToken: cancellationToken));
                justCompleted = false;
            }

            var updated = await connection.QuerySingleAsync<UserMissionEntity>(new CommandDefinition(
                UserMissionSelect + " WHERE id = @Id",
                new { Id = missionId },
                transaction: tx,
                cancellationToken: cancellationToken));

            // Only treat as just-completed if we transitioned in this call
            var transitioned = justCompleted &&
                string.Equals(updated.Status, MissionStatuses.Completed, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mission.Status, MissionStatuses.Completed, StringComparison.OrdinalIgnoreCase);

            await tx.CommitAsync(cancellationToken);
            return (updated, transitioned);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task MarkRewardTransactionAsync(
        Guid missionId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_missions
            SET reward_transaction_id = @TransactionId, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id AND reward_transaction_id IS NULL
            """,
            new { Id = missionId, TransactionId = transactionId },
            cancellationToken: cancellationToken));
    }

    public async Task<(IReadOnlyList<UserMissionEntity> Items, int Total)> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? type,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int FROM user_missions
            WHERE user_id = @UserId
              AND (@Type IS NULL OR type = @Type)
              AND (@Status IS NULL OR status = @Status)
              AND (@From IS NULL OR period_start >= @From)
              AND (@To IS NULL OR period_start <= @To)
            """,
            new
            {
                UserId = userId,
                Type = type,
                Status = status,
                From = from?.UtcDateTime,
                To = to?.UtcDateTime
            },
            cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<UserMissionEntity>(new CommandDefinition(
            UserMissionSelect + """
            WHERE user_id = @UserId
              AND (@Type IS NULL OR type = @Type)
              AND (@Status IS NULL OR status = @Status)
              AND (@From IS NULL OR period_start >= @From)
              AND (@To IS NULL OR period_start <= @To)
            ORDER BY period_start DESC, created_at DESC
            LIMIT @Limit OFFSET @Offset
            """,
            new
            {
                UserId = userId,
                Type = type,
                Status = status,
                From = from?.UtcDateTime,
                To = to?.UtcDateTime,
                Limit = pageSize,
                Offset = offset
            },
            cancellationToken: cancellationToken))).ToList();

        return (items, total);
    }

    public async Task<long> GetUserTotalXpAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COALESCE((SELECT total_xp FROM user_progress WHERE user_id = @UserId), 0)",
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<MissionAnalyticsResponse> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var totals = await connection.QuerySingleAsync(new CommandDefinition(
            """
            SELECT
                COUNT(*)::bigint AS Assigned,
                COUNT(*) FILTER (WHERE status = 'COMPLETED')::bigint AS Completed,
                COUNT(*) FILTER (WHERE status = 'EXPIRED')::bigint AS Expired,
                COALESCE(AVG(CASE WHEN target_value > 0 THEN progress_value::float / target_value ELSE 0 END), 0) AS AvgProgress,
                COALESCE(SUM(CASE WHEN status = 'COMPLETED' THEN reward_xp ELSE 0 END), 0)::bigint AS XpAwarded
            FROM user_missions
            """,
            cancellationToken: cancellationToken));

        var byCode = (await connection.QueryAsync<NamedCountDto>(new CommandDefinition(
            """
            SELECT code AS Name, COUNT(*)::bigint AS Count
            FROM user_missions
            WHERE status = 'COMPLETED'
            GROUP BY code
            ORDER BY Count DESC
            LIMIT 20
            """,
            cancellationToken: cancellationToken))).ToList();

        long assigned = totals.Assigned;
        long completed = totals.Completed;
        long expired = totals.Expired;
        return new MissionAnalyticsResponse(
            assigned,
            completed,
            expired,
            assigned == 0 ? 0 : Math.Round(100d * completed / assigned, 2),
            assigned == 0 ? 0 : Math.Round(100d * expired / assigned, 2),
            Math.Round((double)totals.AvgProgress * 100, 2),
            (long)totals.XpAwarded,
            byCode);
    }

    private const string UserMissionSelect = """
        SELECT id AS Id, user_id AS UserId, mission_template_id AS MissionTemplateId,
               code AS Code, type AS Type, title AS Title, description AS Description,
               requirement_type AS RequirementType, period_start AS PeriodStart, period_end AS PeriodEnd,
               progress_value AS ProgressValue, target_value AS TargetValue, reward_xp AS RewardXp,
               status AS Status, completed_at AS CompletedAt, reward_transaction_id AS RewardTransactionId,
               created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM user_missions
        """;
}

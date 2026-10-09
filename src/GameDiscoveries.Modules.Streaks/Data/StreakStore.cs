using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Streaks.Domain;
using GameDiscoveries.Modules.Streaks.Models;
using GameDiscoveries.Modules.Streaks.Options;
using GameDiscoveries.Modules.Streaks.Services;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Streaks.Data;

public interface IQualifyingActivityService
{
    /// <summary>
    /// Records a valid game session as qualifying activity for the local calendar day.
    /// Returns null if session does not qualify. WasNewDay indicates first qualification that day.
    /// </summary>
    Task<ActivityDayRow?> RecordValidSessionAsync(
        Guid userId,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActivityOnDateAsync(Guid userId, DateOnly activityDate, CancellationToken cancellationToken = default);
}

public interface IStreakStore
{
    Task EnsureProgressRowAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserStreakRow> GetStreakAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveStreakAsync(UserStreakRow row, CancellationToken cancellationToken = default);

    Task InsertHistoryAsync(
        Guid userId,
        string eventType,
        int streakValue,
        DateOnly? activityDate,
        int? previousStreak,
        int? newStreak,
        string? reason,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreakMilestoneEntity>> GetActiveMilestonesAsync(CancellationToken cancellationToken = default);

    Task<bool> HasAchievedMilestoneAsync(Guid userId, int days, CancellationToken cancellationToken = default);

    Task RecordMilestoneAsync(Guid userId, int days, int rewardXp, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<StreakHistoryItemDto> Items, int Total)> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? eventType,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);

    Task<AdminStreakOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<int> CountActivityDaysInRangeAsync(
        Guid userId,
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);
}

public sealed class QualifyingActivityService(
    IDbConnectionFactory connectionFactory,
    IStreakTimeService time,
    IOptions<StreakOptions> options) : IQualifyingActivityService
{
    public async Task<ActivityDayRow?> RecordValidSessionAsync(
        Guid userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return null;
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var session = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT session_id AS SessionId, user_id AS UserId, is_valid AS IsValid,
                   active_seconds AS ActiveSeconds,
                   COALESCE(ended_at, updated_at, created_at) AS OccurredAt
            FROM game_play_sessions
            WHERE session_id = @SessionId
            """,
            new { SessionId = sessionId },
            cancellationToken: cancellationToken));

        if (session is null)
        {
            return null;
        }

        if (session.UserId is null || (Guid)session.UserId != userId)
        {
            return null;
        }

        if (!(bool)session.IsValid || (int)session.ActiveSeconds < options.Value.MinimumActiveSeconds)
        {
            return null;
        }

        var occurredAt = new DateTimeOffset(DateTime.SpecifyKind((DateTime)session.OccurredAt, DateTimeKind.Utc));
        var activityDate = time.GetLocalDate(occurredAt);
        var activityDateDto = activityDate.ToDateTime(TimeOnly.MinValue);

        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<ActivityDayRow>(new CommandDefinition(
                """
                SELECT id AS Id, user_id AS UserId, activity_date AS ActivityDate,
                       qualifying_session_count AS QualifyingSessionCount
                FROM user_activity_days
                WHERE user_id = @UserId AND activity_date = @ActivityDate
                FOR UPDATE
                """,
                new { UserId = userId, ActivityDate = activityDateDto },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (existing is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE user_activity_days
                    SET qualifying_session_count = qualifying_session_count + 1,
                        last_activity_at = @OccurredAt,
                        updated_at = (NOW() AT TIME ZONE 'utc')
                    WHERE id = @Id
                    """,
                    new { existing.Id, OccurredAt = occurredAt.UtcDateTime },
                    transaction: tx,
                    cancellationToken: cancellationToken));

                existing.QualifyingSessionCount += 1;
                existing.WasNewDay = false;
                await tx.CommitAsync(cancellationToken);
                return existing;
            }

            var id = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO user_activity_days (
                    id, user_id, activity_date, first_activity_at, last_activity_at,
                    qualifying_session_count, created_at, updated_at)
                VALUES (
                    @Id, @UserId, @ActivityDate, @OccurredAt, @OccurredAt, 1,
                    (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
                ON CONFLICT (user_id, activity_date) DO UPDATE SET
                    qualifying_session_count = user_activity_days.qualifying_session_count + 1,
                    last_activity_at = EXCLUDED.last_activity_at,
                    updated_at = (NOW() AT TIME ZONE 'utc')
                """,
                new { Id = id, UserId = userId, ActivityDate = activityDateDto, OccurredAt = occurredAt.UtcDateTime },
                transaction: tx,
                cancellationToken: cancellationToken));

            // Detect if insert won or conflict occurred
            var row = await connection.QuerySingleAsync<ActivityDayRow>(new CommandDefinition(
                """
                SELECT id AS Id, user_id AS UserId, activity_date AS ActivityDate,
                       qualifying_session_count AS QualifyingSessionCount
                FROM user_activity_days
                WHERE user_id = @UserId AND activity_date = @ActivityDate
                """,
                new { UserId = userId, ActivityDate = activityDateDto },
                transaction: tx,
                cancellationToken: cancellationToken));

            row.WasNewDay = row.QualifyingSessionCount == 1;
            await tx.CommitAsync(cancellationToken);
            return row;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> HasActivityOnDateAsync(
        Guid userId,
        DateOnly activityDate,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS(
                SELECT 1 FROM user_activity_days
                WHERE user_id = @UserId AND activity_date = @ActivityDate)
            """,
            new { UserId = userId, ActivityDate = activityDate.ToDateTime(TimeOnly.MinValue) },
            cancellationToken: cancellationToken));
    }
}

public sealed class StreakStore(IDbConnectionFactory connectionFactory) : IStreakStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task EnsureProgressRowAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_progress (user_id, total_xp, level, current_level_xp, current_streak, longest_streak,
                                       streak_status, streak_freeze_count, created_at, updated_at)
            VALUES (@UserId, 0, 1, 0, 0, 0, 'BROKEN', 0, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (user_id) DO NOTHING
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<UserStreakRow> GetStreakAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureProgressRowAsync(userId, cancellationToken);
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<UserStreakRow>(new CommandDefinition(
            """
            SELECT user_id AS UserId,
                   COALESCE(current_streak, 0) AS CurrentStreak,
                   COALESCE(longest_streak, 0) AS LongestStreak,
                   streak_start_date AS StreakStartDate,
                   last_qualifying_activity_date AS LastQualifyingActivityDate,
                   COALESCE(streak_status, 'BROKEN') AS StreakStatus,
                   COALESCE(streak_freeze_count, 0) AS StreakFreezeCount
            FROM user_progress WHERE user_id = @UserId
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return row;
    }

    public async Task SaveStreakAsync(UserStreakRow row, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_progress SET
                current_streak = @CurrentStreak,
                longest_streak = @LongestStreak,
                streak_start_date = @StreakStartDate,
                last_qualifying_activity_date = @LastQualifyingActivityDate,
                streak_status = @StreakStatus,
                streak_freeze_count = @StreakFreezeCount,
                streak_updated_at = (NOW() AT TIME ZONE 'utc'),
                updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE user_id = @UserId
            """,
            new
            {
                row.UserId,
                row.CurrentStreak,
                row.LongestStreak,
                StreakStartDate = row.StreakStartDate,
                LastQualifyingActivityDate = row.LastQualifyingActivityDate,
                row.StreakStatus,
                row.StreakFreezeCount
            },
            cancellationToken: cancellationToken));
    }

    public async Task InsertHistoryAsync(
        Guid userId,
        string eventType,
        int streakValue,
        DateOnly? activityDate,
        int? previousStreak,
        int? newStreak,
        string? reason,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO streak_history (
                id, user_id, event_type, streak_value, activity_date, previous_streak, new_streak,
                reason, metadata_json, created_at)
            VALUES (
                @Id, @UserId, @EventType, @StreakValue, @ActivityDate, @PreviousStreak, @NewStreak,
                @Reason, CAST(@Metadata AS jsonb), (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = eventType,
                StreakValue = streakValue,
                ActivityDate = activityDate?.ToDateTime(TimeOnly.MinValue),
                PreviousStreak = previousStreak,
                NewStreak = newStreak,
                Reason = reason,
                Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions)
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<StreakMilestoneEntity>> GetActiveMilestonesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StreakMilestoneEntity>(new CommandDefinition(
            """
            SELECT id AS Id, days AS Days, title AS Title, description AS Description,
                   reward_xp AS RewardXp, is_active AS IsActive
            FROM streak_milestones
            WHERE is_active = TRUE
            ORDER BY days
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<bool> HasAchievedMilestoneAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS(SELECT 1 FROM user_streak_milestones WHERE user_id = @UserId AND days = @Days)
            """,
            new { UserId = userId, Days = days },
            cancellationToken: cancellationToken));
    }

    public async Task RecordMilestoneAsync(
        Guid userId,
        int days,
        int rewardXp,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_streak_milestones (id, user_id, days, achieved_at, reward_xp)
            VALUES (@Id, @UserId, @Days, (NOW() AT TIME ZONE 'utc'), @RewardXp)
            ON CONFLICT (user_id, days) DO NOTHING
            """,
            new { Id = Guid.NewGuid(), UserId = userId, Days = days, RewardXp = rewardXp },
            cancellationToken: cancellationToken));
    }

    public async Task<(IReadOnlyList<StreakHistoryItemDto> Items, int Total)> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? eventType,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int FROM streak_history
            WHERE user_id = @UserId
              AND (@EventType IS NULL OR event_type = @EventType)
              AND (@From::date IS NULL OR activity_date >= @From::date)
              AND (@To::date IS NULL OR activity_date <= @To::date)
            """,
            new
            {
                UserId = userId,
                EventType = eventType,
                From = from?.ToDateTime(TimeOnly.MinValue),
                To = to?.ToDateTime(TimeOnly.MinValue)
            },
            cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT id, event_type, streak_value, activity_date, previous_streak, new_streak, reason, created_at
            FROM streak_history
            WHERE user_id = @UserId
              AND (@EventType IS NULL OR event_type = @EventType)
              AND (@From::date IS NULL OR activity_date >= @From::date)
              AND (@To::date IS NULL OR activity_date <= @To::date)
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset
            """,
            new
            {
                UserId = userId,
                EventType = eventType,
                From = from?.ToDateTime(TimeOnly.MinValue),
                To = to?.ToDateTime(TimeOnly.MinValue),
                Limit = pageSize,
                Offset = offset
            },
            cancellationToken: cancellationToken));

        var items = rows.Select(r => new StreakHistoryItemDto(
            (Guid)r.id,
            (string)r.event_type,
            (int)r.streak_value,
            r.activity_date is null ? null : DateOnly.FromDateTime((DateTime)r.activity_date),
            (int?)r.previous_streak,
            (int?)r.new_streak,
            (string?)r.reason,
            new DateTimeOffset(DateTime.SpecifyKind((DateTime)r.created_at, DateTimeKind.Utc)))).ToList();

        return (items, total);
    }

    public async Task<AdminStreakOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync(new CommandDefinition(
            """
            SELECT
                COUNT(*) FILTER (WHERE current_streak > 0 AND streak_status IN ('ACTIVE','AT_RISK','FROZEN'))::bigint AS ActiveUsers,
                COALESCE(AVG(current_streak) FILTER (WHERE current_streak > 0), 0) AS AvgCurrent,
                COALESCE(AVG(longest_streak) FILTER (WHERE longest_streak > 0), 0) AS AvgLongest,
                COUNT(*) FILTER (WHERE current_streak = 1)::bigint AS At1,
                COUNT(*) FILTER (WHERE current_streak >= 7)::bigint AS At7,
                COUNT(*) FILTER (WHERE current_streak >= 30)::bigint AS At30,
                (SELECT COUNT(*) FROM streak_history WHERE event_type = 'STREAK_FROZEN')::bigint AS Freezes,
                (SELECT COUNT(*) FROM user_streak_milestones)::bigint AS Milestones
            FROM user_progress
            """,
            cancellationToken: cancellationToken));

        return new AdminStreakOverviewDto(
            (long)row.ActiveUsers,
            Math.Round((double)row.AvgCurrent, 2),
            Math.Round((double)row.AvgLongest, 2),
            (long)row.At1,
            (long)row.At7,
            (long)row.At30,
            (long)row.Freezes,
            (long)row.Milestones);
    }

    public async Task<int> CountActivityDaysInRangeAsync(
        Guid userId,
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int FROM user_activity_days
            WHERE user_id = @UserId
              AND activity_date >= @From
              AND activity_date <= @To
            """,
            new
            {
                UserId = userId,
                From = fromInclusive.ToDateTime(TimeOnly.MinValue),
                To = toInclusive.ToDateTime(TimeOnly.MinValue)
            },
            cancellationToken: cancellationToken));
    }
}

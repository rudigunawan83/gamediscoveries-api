using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;

namespace GameDiscoveries.Modules.Analytics.Data;

public interface IGamePlaySessionStore
{
    Task<bool> GameIsPublishedAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task<GamePlaySessionEntity?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GamePlaySessionEntity>> GetOpenSessionsForIdentityAndGameAsync(
        Guid? userId,
        Guid? anonymousId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task InsertAsync(GamePlaySessionEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(GamePlaySessionEntity entity, CancellationToken cancellationToken = default);

    Task<GamePlaySessionOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
}

public sealed class GamePlaySessionStore(IDbConnectionFactory connectionFactory) : IGamePlaySessionStore
{
    public async Task<bool> GameIsPublishedAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM games WHERE id = @GameId AND status = 'published')",
            new { GameId = gameId },
            cancellationToken: cancellationToken));
    }

    public async Task<GamePlaySessionEntity?> GetBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<GamePlaySessionEntity>(new CommandDefinition(
            """
            SELECT
                id AS Id,
                session_id AS SessionId,
                user_id AS UserId,
                anonymous_id AS AnonymousId,
                game_id AS GameId,
                status AS Status,
                started_at AS StartedAt,
                last_heartbeat_at AS LastHeartbeatAt,
                paused_at AS PausedAt,
                resumed_at AS ResumedAt,
                ended_at AS EndedAt,
                duration_seconds AS DurationSeconds,
                active_seconds AS ActiveSeconds,
                accumulated_active_ms AS AccumulatedActiveMs,
                is_valid AS IsValid,
                invalid_reason AS InvalidReason,
                source AS Source,
                platform AS Platform,
                device_type AS DeviceType,
                app_version AS AppVersion,
                pause_reason AS PauseReason,
                end_reason AS EndReason,
                last_heartbeat_analytics_at AS LastHeartbeatAnalyticsAt,
                heartbeat_count AS HeartbeatCount,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM game_play_sessions
            WHERE session_id = @SessionId
            """,
            new { SessionId = sessionId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<GamePlaySessionEntity>> GetOpenSessionsForIdentityAndGameAsync(
        Guid? userId,
        Guid? anonymousId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<GamePlaySessionEntity>(new CommandDefinition(
            """
            SELECT
                id AS Id,
                session_id AS SessionId,
                user_id AS UserId,
                anonymous_id AS AnonymousId,
                game_id AS GameId,
                status AS Status,
                started_at AS StartedAt,
                last_heartbeat_at AS LastHeartbeatAt,
                paused_at AS PausedAt,
                resumed_at AS ResumedAt,
                ended_at AS EndedAt,
                duration_seconds AS DurationSeconds,
                active_seconds AS ActiveSeconds,
                accumulated_active_ms AS AccumulatedActiveMs,
                is_valid AS IsValid,
                invalid_reason AS InvalidReason,
                source AS Source,
                platform AS Platform,
                device_type AS DeviceType,
                app_version AS AppVersion,
                pause_reason AS PauseReason,
                end_reason AS EndReason,
                last_heartbeat_analytics_at AS LastHeartbeatAnalyticsAt,
                heartbeat_count AS HeartbeatCount,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM game_play_sessions
            WHERE game_id = @GameId
              AND status = ANY(@OpenStatuses)
              AND (
                    (@UserId IS NOT NULL AND user_id = @UserId)
                 OR (@AnonymousId IS NOT NULL AND anonymous_id = @AnonymousId)
              )
            """,
            new
            {
                GameId = gameId,
                UserId = userId,
                AnonymousId = anonymousId,
                OpenStatuses = GamePlaySessionStatuses.Open.ToArray()
            },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task InsertAsync(GamePlaySessionEntity entity, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            INSERT INTO game_play_sessions (
                id, session_id, user_id, anonymous_id, game_id, status,
                started_at, last_heartbeat_at, paused_at, resumed_at, ended_at,
                duration_seconds, active_seconds, accumulated_active_ms,
                is_valid, invalid_reason, source, platform, device_type, app_version,
                pause_reason, end_reason, last_heartbeat_analytics_at, heartbeat_count,
                created_at, updated_at)
            VALUES (
                @Id, @SessionId, @UserId, @AnonymousId, @GameId, @Status,
                @StartedAt, @LastHeartbeatAt, @PausedAt, @ResumedAt, @EndedAt,
                @DurationSeconds, @ActiveSeconds, @AccumulatedActiveMs,
                @IsValid, @InvalidReason, @Source, @Platform, @DeviceType, @AppVersion,
                @PauseReason, @EndReason, @LastHeartbeatAnalyticsAt, @HeartbeatCount,
                @CreatedAt, @UpdatedAt);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, ToParams(entity), cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(GamePlaySessionEntity entity, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE game_play_sessions SET
                status = @Status,
                last_heartbeat_at = @LastHeartbeatAt,
                paused_at = @PausedAt,
                resumed_at = @ResumedAt,
                ended_at = @EndedAt,
                duration_seconds = @DurationSeconds,
                active_seconds = @ActiveSeconds,
                accumulated_active_ms = @AccumulatedActiveMs,
                is_valid = @IsValid,
                invalid_reason = @InvalidReason,
                pause_reason = @PauseReason,
                end_reason = @EndReason,
                last_heartbeat_analytics_at = @LastHeartbeatAnalyticsAt,
                heartbeat_count = @HeartbeatCount,
                updated_at = @UpdatedAt
            WHERE session_id = @SessionId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, ToParams(entity), cancellationToken: cancellationToken));
    }

    public async Task<GamePlaySessionOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        var totals = await connection.QuerySingleAsync<OverviewRow>(new CommandDefinition(
            """
            SELECT
                COUNT(*) FILTER (WHERE started_at >= date_trunc('day', NOW() AT TIME ZONE 'utc'))::bigint AS SessionsToday,
                COUNT(*) FILTER (WHERE status IN ('STARTED', 'ACTIVE', 'PAUSED'))::bigint AS ActiveSessions,
                COUNT(*) FILTER (WHERE is_valid = TRUE)::bigint AS ValidSessions,
                COUNT(*) FILTER (WHERE status = 'INVALID' OR (status = 'ENDED' AND is_valid = FALSE))::bigint AS InvalidSessions,
                COALESCE(AVG(duration_seconds) FILTER (WHERE status = 'ENDED'), 0)::double precision AS AverageDurationSeconds,
                COALESCE(AVG(active_seconds) FILTER (WHERE status = 'ENDED'), 0)::double precision AS AverageActiveSeconds,
                CASE
                    WHEN COUNT(*) FILTER (WHERE status IN ('ENDED', 'INVALID')) = 0 THEN 0
                    ELSE (COUNT(*) FILTER (WHERE status = 'ENDED')::double precision
                          / COUNT(*) FILTER (WHERE status IN ('ENDED', 'INVALID'))::double precision)
                END AS CompletionRate
            FROM game_play_sessions;
            """,
            cancellationToken: cancellationToken));

        var topGames = (await connection.QueryAsync<AnalyticsNamedCount>(new CommandDefinition(
            """
            SELECT g.title AS Name, COUNT(*)::bigint AS Count
            FROM game_play_sessions s
            INNER JOIN games g ON g.id = s.game_id
            WHERE s.started_at >= (NOW() AT TIME ZONE 'utc') - INTERVAL '7 days'
            GROUP BY g.title
            ORDER BY Count DESC
            LIMIT 10;
            """,
            cancellationToken: cancellationToken))).ToList();

        return new GamePlaySessionOverview(
            totals.SessionsToday,
            totals.ActiveSessions,
            totals.ValidSessions,
            totals.InvalidSessions,
            totals.AverageDurationSeconds,
            totals.AverageActiveSeconds,
            totals.CompletionRate,
            topGames);
    }

    private static object ToParams(GamePlaySessionEntity e) => new
    {
        e.Id,
        e.SessionId,
        e.UserId,
        e.AnonymousId,
        e.GameId,
        e.Status,
        StartedAt = e.StartedAt.UtcDateTime,
        LastHeartbeatAt = e.LastHeartbeatAt?.UtcDateTime,
        PausedAt = e.PausedAt?.UtcDateTime,
        ResumedAt = e.ResumedAt?.UtcDateTime,
        EndedAt = e.EndedAt?.UtcDateTime,
        e.DurationSeconds,
        e.ActiveSeconds,
        e.AccumulatedActiveMs,
        e.IsValid,
        e.InvalidReason,
        e.Source,
        e.Platform,
        e.DeviceType,
        e.AppVersion,
        e.PauseReason,
        e.EndReason,
        LastHeartbeatAnalyticsAt = e.LastHeartbeatAnalyticsAt?.UtcDateTime,
        e.HeartbeatCount,
        CreatedAt = e.CreatedAt.UtcDateTime,
        UpdatedAt = e.UpdatedAt.UtcDateTime
    };

    private sealed class OverviewRow
    {
        public long SessionsToday { get; init; }
        public long ActiveSessions { get; init; }
        public long ValidSessions { get; init; }
        public long InvalidSessions { get; init; }
        public double AverageDurationSeconds { get; init; }
        public double AverageActiveSeconds { get; init; }
        public double CompletionRate { get; init; }
    }
}

using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Analytics.Models;

namespace GameDiscoveries.Modules.Analytics.Data;

public interface IAnalyticsEventStore
{
    Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetExistingEventIdsAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken = default);

    Task InsertAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default);

    Task<int> TryInsertAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default);

    Task InsertManyAsync(
        IReadOnlyList<AnalyticsEventWriteCommand> commands,
        CancellationToken cancellationToken = default);

    Task UpsertSessionAsync(
        string sessionId,
        Guid? userId,
        Guid? anonymousId,
        string source,
        string platform,
        DateTimeOffset activityAt,
        CancellationToken cancellationToken = default);

    Task LinkIdentityAsync(
        Guid anonymousId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> GameExistsAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetExistingGameIdsAsync(
        IReadOnlyCollection<Guid> gameIds,
        CancellationToken cancellationToken = default);

    Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);

    // Legacy bridge used by old TrackAsync callers during transition.
    Task TrackLegacyAsync(
        string eventName,
        Guid? userId,
        string? sessionId,
        Guid? gameId,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken cancellationToken = default);
}

public sealed class AnalyticsEventStore(IDbConnectionFactory connectionFactory) : IAnalyticsEventStore
{
    public async Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM analytics_events WHERE event_id = @EventId)",
            new { EventId = eventId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlySet<Guid>> GetExistingEventIdsAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken = default)
    {
        if (eventIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT event_id FROM analytics_events WHERE event_id = ANY(@EventIds)",
            new { EventIds = eventIds.ToArray() },
            cancellationToken: cancellationToken));
        return rows.ToHashSet();
    }

    public async Task InsertAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
    {
        await TryInsertAsync(command, cancellationToken);
    }

    public async Task<int> TryInsertAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(InsertSql, ToRow(command), cancellationToken: cancellationToken));
    }

    public async Task InsertManyAsync(
        IReadOnlyList<AnalyticsEventWriteCommand> commands,
        CancellationToken cancellationToken = default)
    {
        if (commands.Count == 0)
        {
            return;
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var command in commands)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    InsertSql,
                    ToRow(command),
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpsertSessionAsync(
        string sessionId,
        Guid? userId,
        Guid? anonymousId,
        string source,
        string platform,
        DateTimeOffset activityAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            INSERT INTO analytics_sessions (
                id, session_id, user_id, anonymous_id, source, platform,
                started_at, last_activity_at, ended_at, created_at)
            VALUES (
                @Id, @SessionId, @UserId, @AnonymousId, @Source, @Platform,
                @ActivityAt, @ActivityAt, NULL, (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (session_id) DO UPDATE SET
                user_id = COALESCE(EXCLUDED.user_id, analytics_sessions.user_id),
                anonymous_id = COALESCE(EXCLUDED.anonymous_id, analytics_sessions.anonymous_id),
                last_activity_at = GREATEST(analytics_sessions.last_activity_at, EXCLUDED.last_activity_at),
                source = EXCLUDED.source,
                platform = EXCLUDED.platform;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                AnonymousId = anonymousId,
                Source = source,
                Platform = platform,
                ActivityAt = activityAt.UtcDateTime
            },
            cancellationToken: cancellationToken));
    }

    public async Task LinkIdentityAsync(
        Guid anonymousId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        const string sql = """
            INSERT INTO analytics_identity_links (id, anonymous_id, user_id, first_seen_at, last_seen_at)
            VALUES (@Id, @AnonymousId, @UserId, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (anonymous_id, user_id) DO UPDATE SET
                last_seen_at = (NOW() AT TIME ZONE 'utc');
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = Guid.NewGuid(), AnonymousId = anonymousId, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> GameExistsAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM games WHERE id = @GameId)",
            new { GameId = gameId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlySet<Guid>> GetExistingGameIdsAsync(
        IReadOnlyCollection<Guid> gameIds,
        CancellationToken cancellationToken = default)
    {
        if (gameIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT id FROM games WHERE id = ANY(@GameIds)",
            new { GameIds = gameIds.ToArray() },
            cancellationToken: cancellationToken));
        return rows.ToHashSet();
    }

    public async Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        var totals = await connection.QuerySingleAsync<OverviewTotalsRow>(new CommandDefinition(
            """
            SELECT
                COUNT(*)::bigint AS TotalEvents,
                COUNT(*) FILTER (WHERE occurred_at >= date_trunc('day', NOW() AT TIME ZONE 'utc'))::bigint AS EventsToday,
                COUNT(DISTINCT user_id) FILTER (
                    WHERE user_id IS NOT NULL
                      AND occurred_at >= date_trunc('day', NOW() AT TIME ZONE 'utc'))::bigint AS ActiveUsersToday,
                COUNT(*) FILTER (WHERE event_name = 'GAME_VIEW')::bigint AS GameViews,
                COUNT(*) FILTER (WHERE event_name = 'GAME_START')::bigint AS GameStarts
            FROM analytics_events;
            """,
            cancellationToken: cancellationToken));

        var sessions = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*)::bigint FROM analytics_sessions",
            cancellationToken: cancellationToken));

        var topGames = (await connection.QueryAsync<AnalyticsNamedCount>(new CommandDefinition(
            """
            SELECT g.title AS Name, COUNT(*)::bigint AS Count
            FROM analytics_events e
            INNER JOIN games g ON g.id = e.game_id
            WHERE e.game_id IS NOT NULL
              AND e.occurred_at >= (NOW() AT TIME ZONE 'utc') - INTERVAL '7 days'
            GROUP BY g.title
            ORDER BY Count DESC
            LIMIT 10;
            """,
            cancellationToken: cancellationToken))).ToList();

        var topTypes = (await connection.QueryAsync<AnalyticsNamedCount>(new CommandDefinition(
            """
            SELECT event_name AS Name, COUNT(*)::bigint AS Count
            FROM analytics_events
            WHERE occurred_at >= (NOW() AT TIME ZONE 'utc') - INTERVAL '7 days'
            GROUP BY event_name
            ORDER BY Count DESC
            LIMIT 10;
            """,
            cancellationToken: cancellationToken))).ToList();

        return new AnalyticsOverviewResponse(
            totals.TotalEvents,
            totals.EventsToday,
            totals.ActiveUsersToday,
            totals.GameViews,
            totals.GameStarts,
            sessions,
            topGames,
            topTypes);
    }

    private sealed class OverviewTotalsRow
    {
        public long TotalEvents { get; init; }
        public long EventsToday { get; init; }
        public long ActiveUsersToday { get; init; }
        public long GameViews { get; init; }
        public long GameStarts { get; init; }
    }

    public async Task TrackLegacyAsync(
        string eventName,
        Guid? userId,
        string? sessionId,
        Guid? gameId,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken cancellationToken = default)
    {
        // Kept for binary compatibility of IAnalyticsEventStore during migration; prefer IAnalyticsEventService.
        await InsertAsync(
            new AnalyticsEventWriteCommand(
                Guid.NewGuid(),
                eventName,
                userId,
                null,
                sessionId,
                gameId,
                "WEB",
                "WEB",
                null,
                null,
                null,
                null,
                properties.ToDictionary(x => x.Key, x => x.Value),
                null,
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private const string InsertSql = """
        INSERT INTO analytics_events (
            id, event_id, event_name, user_id, anonymous_id, session_id, game_id,
            source, platform, device_type, app_version, page_url, referrer_url,
            properties, ip_hash, user_agent, occurred_at, received_at, created_at)
        VALUES (
            @Id, @EventId, @EventType, @UserId, @AnonymousId, @SessionId, @GameId,
            @Source, @Platform, @DeviceType, @AppVersion, @PageUrl, @ReferrerUrl,
            CAST(@MetadataJson AS jsonb), @IpHash, @UserAgent, @OccurredAt, @ReceivedAt,
            (NOW() AT TIME ZONE 'utc'))
        ON CONFLICT (event_id) DO NOTHING;
        """;

    private static object ToRow(AnalyticsEventWriteCommand command) => new
    {
        Id = Guid.NewGuid(),
        command.EventId,
        command.EventType,
        command.UserId,
        command.AnonymousId,
        command.SessionId,
        command.GameId,
        command.Source,
        command.Platform,
        command.DeviceType,
        command.AppVersion,
        command.PageUrl,
        command.ReferrerUrl,
        command.MetadataJson,
        command.IpHash,
        command.UserAgent,
        OccurredAt = command.OccurredAt.UtcDateTime,
        ReceivedAt = command.ReceivedAt.UtcDateTime
    };
}

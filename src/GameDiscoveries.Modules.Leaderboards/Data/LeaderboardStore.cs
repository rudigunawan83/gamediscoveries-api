using System.Data.Common;
using System.Text.Json;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Leaderboards.Domain;

namespace GameDiscoveries.Modules.Leaderboards.Data;

public interface ILeaderboardStore
{
    Task<IReadOnlyList<LeaderboardDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<LeaderboardDefinition?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<LeaderboardPeriod?> GetActivePeriodAsync(Guid leaderboardId, CancellationToken cancellationToken = default);
    Task EnsurePeriodAsync(LeaderboardDefinition definition, DateTimeOffset start, DateTimeOffset end, string code, string timezone, CancellationToken cancellationToken = default);
    Task EndExpiredPeriodsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task UpsertScoreDeltaAsync(Guid leaderboardId, Guid periodId, Guid userId, long delta, bool countSession, DateTimeOffset at, CancellationToken cancellationToken = default);
    Task RecalculateRanksAsync(Guid leaderboardId, Guid periodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardEntryRow>> GetTopAsync(Guid leaderboardId, Guid periodId, int limit, CancellationToken cancellationToken = default);
    Task<LeaderboardEntryRow?> GetUserEntryAsync(Guid leaderboardId, Guid periodId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountParticipantsAsync(Guid leaderboardId, Guid periodId, CancellationToken cancellationToken = default);
    Task<(int? NextRank, long? NextScore)> GetNextRankTargetAsync(Guid leaderboardId, Guid periodId, long score, int? rank, CancellationToken cancellationToken = default);
    Task CreateSnapshotAsync(Guid leaderboardId, Guid periodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetUserHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetPeriodHistoryAsync(Guid leaderboardId, int limit, CancellationToken cancellationToken = default);
    Task SetDisqualifiedAsync(Guid leaderboardId, Guid periodId, Guid userId, bool disqualified, CancellationToken cancellationToken = default);
    Task RebuildPeriodFromXpAsync(Guid leaderboardId, Guid periodId, DateTimeOffset start, DateTimeOffset end, bool includeAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminLeaderboardOverviewDto>> AdminOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionDto>> ListCompetitionsAsync(CancellationToken cancellationToken = default);
    Task JoinCompetitionAsync(string code, Guid userId, CancellationToken cancellationToken = default);
    Task<(int Awarded, int Skipped)> SettleCompetitionAsync(string code, Func<Guid, int, Task<Guid?>> awardReward, CancellationToken cancellationToken = default);
}

public sealed class LeaderboardStore(IDbConnectionFactory connectionFactory) : ILeaderboardStore
{
    public async Task<IReadOnlyList<LeaderboardDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LeaderboardDefinition>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, name AS Name, description AS Description, type AS Type,
                   score_type AS ScoreType, period_type AS PeriodType, scope_type AS ScopeType,
                   scope_value AS ScopeValue, is_active AS IsActive, sort_order AS SortOrder
            FROM leaderboard_definitions
            WHERE is_active = TRUE
            ORDER BY sort_order, code
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<LeaderboardDefinition?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LeaderboardDefinition>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, name AS Name, description AS Description, type AS Type,
                   score_type AS ScoreType, period_type AS PeriodType, scope_type AS ScopeType,
                   scope_value AS ScopeValue, is_active AS IsActive, sort_order AS SortOrder
            FROM leaderboard_definitions
            WHERE UPPER(code) = UPPER(@Code)
            LIMIT 1
            """,
            new { Code = code.Replace('-', '_').Trim() },
            cancellationToken: cancellationToken));
    }

    public async Task<LeaderboardPeriod?> GetActivePeriodAsync(Guid leaderboardId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LeaderboardPeriod>(new CommandDefinition(
            """
            SELECT id AS Id, code AS Code, type AS Type, name AS Name, start_at AS StartAt, end_at AS EndAt,
                   timezone AS Timezone, status AS Status, leaderboard_id AS LeaderboardId, is_public AS IsPublic
            FROM leaderboard_periods
            WHERE leaderboard_id = @LeaderboardId AND status = 'ACTIVE'
            ORDER BY start_at DESC
            LIMIT 1
            """,
            new { LeaderboardId = leaderboardId },
            cancellationToken: cancellationToken));
    }

    public async Task EnsurePeriodAsync(
        LeaderboardDefinition definition,
        DateTimeOffset start,
        DateTimeOffset end,
        string code,
        string timezone,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM leaderboard_periods WHERE code = @Code LIMIT 1",
            new { Code = code },
            tx,
            cancellationToken: cancellationToken));

        if (existing is null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO leaderboard_periods (
                    id, code, type, name, description, start_at, end_at, timezone, status,
                    score_type, is_public, leaderboard_id, created_at, updated_at)
                VALUES (
                    @Id, @Code, @Type, @Name, @Description, @StartAt, @EndAt, @Timezone, 'ACTIVE',
                    'XP', TRUE, @LeaderboardId, (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Type = definition.PeriodType,
                    Name = $"{definition.Name} ({code})",
                    Description = definition.Description,
                    StartAt = start,
                    EndAt = end,
                    Timezone = timezone,
                    LeaderboardId = definition.Id
                },
                tx,
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE leaderboard_periods
                SET status = 'ACTIVE', updated_at = (NOW() AT TIME ZONE 'utc')
                WHERE id = @Id AND status IN ('SCHEDULED', 'ACTIVE')
                """,
                new { Id = existing.Value },
                tx,
                cancellationToken: cancellationToken));
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task EndExpiredPeriodsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE leaderboard_periods
            SET status = 'ENDED', updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE status = 'ACTIVE'
              AND type <> 'ALL_TIME'
              AND end_at <= @UtcNow
            """,
            new { UtcNow = utcNow },
            cancellationToken: cancellationToken));
    }

    public async Task UpsertScoreDeltaAsync(
        Guid leaderboardId,
        Guid periodId,
        Guid userId,
        long delta,
        bool countSession,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (delta == 0) return;

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO leaderboard_entries (
                id, leaderboard_id, period_id, user_id, score, rank, previous_rank, rank_change,
                games_played, valid_sessions, xp_earned, score_reached_at, is_disqualified, created_at, updated_at)
            VALUES (
                @Id, @LeaderboardId, @PeriodId, @UserId, GREATEST(@Delta, 0), NULL, NULL, 0,
                CASE WHEN @CountSession THEN 1 ELSE 0 END,
                CASE WHEN @CountSession THEN 1 ELSE 0 END,
                GREATEST(@Delta, 0), @At, FALSE, @At, @At)
            ON CONFLICT (leaderboard_id, period_id, user_id) DO UPDATE SET
                score = GREATEST(leaderboard_entries.score + @Delta, 0),
                xp_earned = GREATEST(leaderboard_entries.xp_earned + @Delta, 0),
                games_played = leaderboard_entries.games_played + CASE WHEN @CountSession THEN 1 ELSE 0 END,
                valid_sessions = leaderboard_entries.valid_sessions + CASE WHEN @CountSession THEN 1 ELSE 0 END,
                score_reached_at = CASE
                    WHEN @Delta > 0 AND (leaderboard_entries.score + @Delta) > leaderboard_entries.score THEN @At
                    ELSE leaderboard_entries.score_reached_at
                END,
                updated_at = @At
            WHERE leaderboard_entries.is_disqualified = FALSE
            """,
            new
            {
                Id = Guid.NewGuid(),
                LeaderboardId = leaderboardId,
                PeriodId = periodId,
                UserId = userId,
                Delta = delta,
                CountSession = countSession,
                At = at
            },
            cancellationToken: cancellationToken));
    }

    public async Task RecalculateRanksAsync(Guid leaderboardId, Guid periodId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            WITH ranked AS (
                SELECT id,
                       ROW_NUMBER() OVER (
                           ORDER BY score DESC, score_reached_at ASC, valid_sessions DESC, user_id ASC
                       )::int AS new_rank
                FROM leaderboard_entries
                WHERE leaderboard_id = @LeaderboardId
                  AND period_id = @PeriodId
                  AND is_disqualified = FALSE
                  AND score > 0
            )
            UPDATE leaderboard_entries e
            SET previous_rank = CASE
                    WHEN e.rank IS DISTINCT FROM r.new_rank THEN e.rank
                    ELSE e.previous_rank
                END,
                rank_change = CASE
                    WHEN e.rank IS NULL THEN 0
                    WHEN e.rank IS DISTINCT FROM r.new_rank THEN COALESCE(e.rank, r.new_rank) - r.new_rank
                    ELSE e.rank_change
                END,
                rank = r.new_rank,
                updated_at = (NOW() AT TIME ZONE 'utc')
            FROM ranked r
            WHERE e.id = r.id
            """,
            new { LeaderboardId = leaderboardId, PeriodId = periodId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LeaderboardEntryRow>> GetTopAsync(
        Guid leaderboardId,
        Guid periodId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LeaderboardEntryRow>(new CommandDefinition(
            """
            SELECT e.id AS Id, e.leaderboard_id AS LeaderboardId, e.period_id AS PeriodId, e.user_id AS UserId,
                   e.score AS Score, e.rank AS Rank, e.previous_rank AS PreviousRank, e.rank_change AS RankChange,
                   e.games_played AS GamesPlayed, e.valid_sessions AS ValidSessions, e.xp_earned AS XpEarned,
                   e.score_reached_at AS ScoreReachedAt, e.is_disqualified AS IsDisqualified,
                   u.username AS Username, u.display_name AS DisplayName, u.avatar_url AS AvatarUrl,
                   up.level AS Level
            FROM leaderboard_entries e
            INNER JOIN users u ON u.id = e.user_id
            LEFT JOIN user_progress up ON up.user_id = e.user_id
            WHERE e.leaderboard_id = @LeaderboardId
              AND e.period_id = @PeriodId
              AND e.is_disqualified = FALSE
              AND e.score > 0
              AND COALESCE(u.show_on_leaderboards, TRUE) = TRUE
            ORDER BY e.rank NULLS LAST, e.score DESC, e.score_reached_at ASC, e.user_id ASC
            LIMIT @Limit
            """,
            new { LeaderboardId = leaderboardId, PeriodId = periodId, Limit = limit },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<LeaderboardEntryRow?> GetUserEntryAsync(
        Guid leaderboardId,
        Guid periodId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LeaderboardEntryRow>(new CommandDefinition(
            """
            SELECT e.id AS Id, e.leaderboard_id AS LeaderboardId, e.period_id AS PeriodId, e.user_id AS UserId,
                   e.score AS Score, e.rank AS Rank, e.previous_rank AS PreviousRank, e.rank_change AS RankChange,
                   e.games_played AS GamesPlayed, e.valid_sessions AS ValidSessions, e.xp_earned AS XpEarned,
                   e.score_reached_at AS ScoreReachedAt, e.is_disqualified AS IsDisqualified,
                   u.username AS Username, u.display_name AS DisplayName, u.avatar_url AS AvatarUrl,
                   up.level AS Level
            FROM leaderboard_entries e
            INNER JOIN users u ON u.id = e.user_id
            LEFT JOIN user_progress up ON up.user_id = e.user_id
            WHERE e.leaderboard_id = @LeaderboardId AND e.period_id = @PeriodId AND e.user_id = @UserId
            LIMIT 1
            """,
            new { LeaderboardId = leaderboardId, PeriodId = periodId, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> CountParticipantsAsync(Guid leaderboardId, Guid periodId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int FROM leaderboard_entries
            WHERE leaderboard_id = @LeaderboardId AND period_id = @PeriodId
              AND is_disqualified = FALSE AND score > 0
            """,
            new { LeaderboardId = leaderboardId, PeriodId = periodId },
            cancellationToken: cancellationToken));
    }

    public async Task<(int? NextRank, long? NextScore)> GetNextRankTargetAsync(
        Guid leaderboardId,
        Guid periodId,
        long score,
        int? rank,
        CancellationToken cancellationToken = default)
    {
        if (rank is null or <= 1) return (null, null);
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<(int Rank, long Score)?>(new CommandDefinition(
            """
            SELECT rank AS Rank, score AS Score
            FROM leaderboard_entries
            WHERE leaderboard_id = @LeaderboardId AND period_id = @PeriodId
              AND is_disqualified = FALSE AND score > @Score AND rank IS NOT NULL
            ORDER BY rank ASC
            LIMIT 1
            """,
            new { LeaderboardId = leaderboardId, PeriodId = periodId, Score = score },
            cancellationToken: cancellationToken));
        return row is null ? (null, null) : (row.Value.Rank, row.Value.Score);
    }

    public async Task CreateSnapshotAsync(Guid leaderboardId, Guid periodId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var stats = await connection.QuerySingleOrDefaultAsync<(int Total, long Top, double Avg)>(new CommandDefinition(
            """
            SELECT COUNT(*)::int AS Total,
                   COALESCE(MAX(score), 0)::bigint AS Top,
                   COALESCE(AVG(score), 0)::float8 AS Avg
            FROM leaderboard_entries
            WHERE leaderboard_id = @LeaderboardId AND period_id = @PeriodId
              AND is_disqualified = FALSE AND score > 0
            """,
            new { LeaderboardId = leaderboardId, PeriodId = periodId },
            cancellationToken: cancellationToken));

        var top = await GetTopAsync(leaderboardId, periodId, 10, cancellationToken);
        var topJson = JsonSerializer.Serialize(top.Select(t => new { t.Rank, t.UserId, t.Score, t.DisplayName }));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO leaderboard_snapshots (
                id, leaderboard_id, period_id, snapshot_at, total_participants, top_score, average_score, top_rank_data_json, created_at)
            VALUES (
                @Id, @LeaderboardId, @PeriodId, (NOW() AT TIME ZONE 'utc'), @Total, @Top, @Avg,
                CAST(@TopJson AS jsonb), (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                LeaderboardId = leaderboardId,
                PeriodId = periodId,
                Total = stats.Total,
                Top = stats.Top,
                Avg = stats.Avg,
                TopJson = topJson
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetUserHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LeaderboardHistoryItemDto>(new CommandDefinition(
            """
            SELECT d.code AS LeaderboardCode, p.code AS PeriodCode, p.name AS PeriodName,
                   e.rank AS Rank, e.score AS Score, p.start_at AS StartAt, p.end_at AS EndAt
            FROM leaderboard_entries e
            INNER JOIN leaderboard_periods p ON p.id = e.period_id
            INNER JOIN leaderboard_definitions d ON d.id = e.leaderboard_id
            WHERE e.user_id = @UserId AND p.status IN ('ENDED', 'FINALIZED', 'REWARDS_SETTLED')
            ORDER BY p.end_at DESC
            LIMIT @Limit
            """,
            new { UserId = userId, Limit = limit },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetPeriodHistoryAsync(Guid leaderboardId, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LeaderboardHistoryItemDto>(new CommandDefinition(
            """
            SELECT d.code AS LeaderboardCode, p.code AS PeriodCode, p.name AS PeriodName,
                   NULL::int AS Rank, COALESCE(MAX(e.score), 0)::bigint AS Score,
                   p.start_at AS StartAt, p.end_at AS EndAt
            FROM leaderboard_periods p
            INNER JOIN leaderboard_definitions d ON d.id = p.leaderboard_id
            LEFT JOIN leaderboard_entries e ON e.period_id = p.id AND e.is_disqualified = FALSE
            WHERE p.leaderboard_id = @LeaderboardId AND p.status IN ('ENDED', 'FINALIZED', 'REWARDS_SETTLED')
            GROUP BY d.code, p.code, p.name, p.start_at, p.end_at
            ORDER BY p.end_at DESC
            LIMIT @Limit
            """,
            new { LeaderboardId = leaderboardId, Limit = limit },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task SetDisqualifiedAsync(Guid leaderboardId, Guid periodId, Guid userId, bool disqualified, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE leaderboard_entries
            SET is_disqualified = @Disqualified,
                rank = CASE WHEN @Disqualified THEN NULL ELSE rank END,
                updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE leaderboard_id = @LeaderboardId AND period_id = @PeriodId AND user_id = @UserId
            """,
            new { LeaderboardId = leaderboardId, PeriodId = periodId, UserId = userId, Disqualified = disqualified },
            cancellationToken: cancellationToken));
        await RecalculateRanksAsync(leaderboardId, periodId, cancellationToken);
    }

    public async Task RebuildPeriodFromXpAsync(
        Guid leaderboardId,
        Guid periodId,
        DateTimeOffset start,
        DateTimeOffset end,
        bool includeAdmin,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM leaderboard_entries WHERE leaderboard_id = @LeaderboardId AND period_id = @PeriodId",
            new { LeaderboardId = leaderboardId, PeriodId = periodId },
            tx,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO leaderboard_entries (
                id, leaderboard_id, period_id, user_id, score, rank, previous_rank, rank_change,
                games_played, valid_sessions, xp_earned, score_reached_at, is_disqualified, created_at, updated_at)
            SELECT
                gen_random_uuid(),
                @LeaderboardId,
                @PeriodId,
                t.user_id,
                GREATEST(SUM(t.xp_amount), 0),
                NULL, NULL, 0,
                COUNT(*) FILTER (WHERE t.event_type = 'GAME_SESSION_END' AND t.xp_amount > 0)::int,
                COUNT(*) FILTER (WHERE t.event_type = 'GAME_SESSION_END' AND t.xp_amount > 0)::int,
                GREATEST(SUM(t.xp_amount), 0),
                COALESCE(MAX(t.created_at) FILTER (WHERE t.xp_amount > 0), (NOW() AT TIME ZONE 'utc')),
                FALSE,
                (NOW() AT TIME ZONE 'utc'),
                (NOW() AT TIME ZONE 'utc')
            FROM xp_transactions t
            WHERE t.created_at >= @StartAt AND t.created_at < @EndAt
              AND t.rule_code NOT IN ('COMPETITION_REWARD')
              AND t.event_type NOT IN ('COMPETITION_REWARD')
              AND (
                    t.rule_code = 'XP_REVERSAL'
                    OR t.event_type = 'XP_REVERSAL'
                    OR (
                        t.rule_code <> 'ADMIN_ADJUSTMENT'
                        AND t.event_type <> 'ADMIN_ADJUSTMENT'
                    )
                    OR (@IncludeAdmin AND (t.rule_code = 'ADMIN_ADJUSTMENT' OR t.event_type = 'ADMIN_ADJUSTMENT'))
                  )
            GROUP BY t.user_id
            HAVING SUM(t.xp_amount) > 0
            """,
            new
            {
                LeaderboardId = leaderboardId,
                PeriodId = periodId,
                StartAt = start,
                EndAt = end,
                IncludeAdmin = includeAdmin
            },
            tx,
            cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);
        await RecalculateRanksAsync(leaderboardId, periodId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminLeaderboardOverviewDto>> AdminOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AdminLeaderboardOverviewDto>(new CommandDefinition(
            """
            SELECT d.code AS Code, d.name AS Name, p.code AS PeriodCode, p.status AS PeriodStatus,
                   p.start_at AS StartAt, p.end_at AS EndAt,
                   COALESCE(COUNT(e.id) FILTER (WHERE e.score > 0 AND e.is_disqualified = FALSE), 0)::int AS Participants,
                   COALESCE(MAX(e.score), 0)::bigint AS TopScore,
                   COALESCE(AVG(e.score) FILTER (WHERE e.score > 0), 0)::float8 AS AverageScore
            FROM leaderboard_definitions d
            LEFT JOIN leaderboard_periods p ON p.leaderboard_id = d.id AND p.status = 'ACTIVE'
            LEFT JOIN leaderboard_entries e ON e.period_id = p.id
            WHERE d.is_active = TRUE
            GROUP BY d.code, d.name, p.code, p.status, p.start_at, p.end_at, d.sort_order
            ORDER BY d.sort_order
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CompetitionDto>> ListCompetitionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CompetitionDto>(new CommandDefinition(
            """
            SELECT c.code AS Code, c.name AS Name, c.description AS Description, c.status AS Status,
                   c.start_at AS StartAt, c.end_at AS EndAt, d.code AS LeaderboardCode, c.requires_join AS RequiresJoin
            FROM competitions c
            INNER JOIN leaderboard_definitions d ON d.id = c.leaderboard_id
            WHERE c.is_public = TRUE AND c.status IN ('SCHEDULED', 'ACTIVE', 'ENDED', 'SETTLED')
            ORDER BY c.start_at DESC
            LIMIT 50
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task JoinCompetitionAsync(string code, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var competition = await connection.QuerySingleOrDefaultAsync<(Guid Id, bool RequiresJoin, string Status)?>(new CommandDefinition(
            """
            SELECT id AS Id, requires_join AS RequiresJoin, status AS Status
            FROM competitions WHERE UPPER(code) = UPPER(@Code) LIMIT 1
            """,
            new { Code = code },
            cancellationToken: cancellationToken));

        if (competition is null)
        {
            throw new InvalidOperationException("Competition not found.");
        }

        if (competition.Value.Status is not ("ACTIVE" or "SCHEDULED"))
        {
            throw new InvalidOperationException("Competition is not open for joining.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO competition_participants (id, competition_id, user_id, joined_at, status)
            VALUES (@Id, @CompetitionId, @UserId, (NOW() AT TIME ZONE 'utc'), 'ACTIVE')
            ON CONFLICT (competition_id, user_id) DO NOTHING
            """,
            new { Id = Guid.NewGuid(), CompetitionId = competition.Value.Id, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<(int Awarded, int Skipped)> SettleCompetitionAsync(
        string code,
        Func<Guid, int, Task<Guid?>> awardReward,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var competition = await connection.QuerySingleOrDefaultAsync<(Guid Id, Guid LeaderboardId, Guid? PeriodId, string? RewardConfig)?>(new CommandDefinition(
            """
            SELECT id AS Id, leaderboard_id AS LeaderboardId, period_id AS PeriodId, reward_config_json::text AS RewardConfig
            FROM competitions WHERE UPPER(code) = UPPER(@Code) LIMIT 1
            """,
            new { Code = code },
            cancellationToken: cancellationToken));

        if (competition is null) throw new InvalidOperationException("Competition not found.");

        var periodId = competition.Value.PeriodId
                       ?? (await GetActivePeriodAsync(competition.Value.LeaderboardId, cancellationToken))?.Id;
        if (periodId is null) throw new InvalidOperationException("Competition period missing.");

        var top = await GetTopAsync(competition.Value.LeaderboardId, periodId.Value, 100, cancellationToken);
        var rewards = ParseRewards(competition.Value.RewardConfig);
        var awarded = 0;
        var skipped = 0;

        foreach (var entry in top)
        {
            if (entry.Rank is null) continue;
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(*)::int FROM competition_results
                WHERE competition_id = @CompetitionId AND user_id = @UserId
                """,
                new { CompetitionId = competition.Value.Id, UserId = entry.UserId },
                cancellationToken: cancellationToken));
            if (exists > 0)
            {
                skipped++;
                continue;
            }

            var xpReward = rewards.TryGetValue(entry.Rank.Value, out var amount) ? amount : 0;
            Guid? txId = null;
            var status = "NOT_ELIGIBLE";
            if (xpReward > 0)
            {
                txId = await awardReward(entry.UserId, xpReward);
                status = txId is null ? "FAILED" : "AWARDED";
                if (txId is not null) awarded++;
                else skipped++;
            }
            else
            {
                skipped++;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO competition_results (
                    id, competition_id, user_id, final_rank, final_score, reward_status, reward_transaction_id, settled_at)
                VALUES (
                    @Id, @CompetitionId, @UserId, @FinalRank, @FinalScore, @RewardStatus, @RewardTransactionId,
                    (NOW() AT TIME ZONE 'utc'))
                ON CONFLICT (competition_id, user_id) DO NOTHING
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competition.Value.Id,
                    UserId = entry.UserId,
                    FinalRank = entry.Rank.Value,
                    FinalScore = entry.Score,
                    RewardStatus = status,
                    RewardTransactionId = txId
                },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE competitions
            SET status = 'SETTLED', updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @Id
            """,
            new { Id = competition.Value.Id },
            cancellationToken: cancellationToken));

        return (awarded, skipped);
    }

    private static Dictionary<int, int> ParseRewards(string? json)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(json))
        {
            result[1] = 1000;
            result[2] = 500;
            result[3] = 250;
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ranks", out var ranks) && ranks.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in ranks.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out var rank) && prop.Value.TryGetInt32(out var xp))
                    {
                        result[rank] = xp;
                    }
                }
            }
        }
        catch
        {
            result[1] = 1000;
            result[2] = 500;
            result[3] = 250;
        }

        return result;
    }
}

using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Xp.Data;
using GameDiscoveries.Modules.Xp.Models;

namespace GameDiscoveries.Modules.Xp.Services;

public interface IAdminGamificationService
{
    Task<GamificationOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AdminUserListItem> Items, int Total)> ListUsersAsync(
        string? search,
        int? level,
        string? status,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailResponse> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<XpAwardResult> AdjustXpAsync(
        Guid userId,
        Guid adminId,
        int amount,
        string reason,
        CancellationToken cancellationToken = default);

    Task ResetGamificationAsync(Guid userId, Guid adminId, string reason, CancellationToken cancellationToken = default);

    Task ResetStreakAsync(Guid userId, Guid adminId, string? reason, CancellationToken cancellationToken = default);

    Task SetUserStatusAsync(Guid userId, Guid adminId, string status, string? reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LevelDefinitionDto>> ListLevelsAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<LevelDefinitionDto> CreateLevelAsync(Guid adminId, UpsertLevelRequest request, CancellationToken cancellationToken = default);

    Task<LevelDefinitionDto> UpdateLevelAsync(Guid adminId, int level, UpsertLevelRequest request, CancellationToken cancellationToken = default);

    Task SetLevelActiveAsync(Guid adminId, int level, bool isActive, CancellationToken cancellationToken = default);
}

public sealed class AdminGamificationService(
    IDbConnectionFactory connectionFactory,
    IXpStore xpStore,
    IXpEngine xpEngine,
    ILevelService levelService,
    IAuditLogService auditLog) : IAdminGamificationService
{
    public async Task<GamificationOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        var totals = await connection.QuerySingleAsync(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*) FROM users) AS TotalUsers,
                (SELECT COUNT(*) FROM user_progress WHERE total_xp > 0) AS UsersWithXp,
                (SELECT COALESCE(SUM(CASE WHEN xp_amount > 0 THEN xp_amount ELSE 0 END), 0) FROM xp_transactions) AS TotalXpAwarded,
                (SELECT COALESCE(SUM(CASE WHEN xp_amount > 0 THEN xp_amount ELSE 0 END), 0)
                 FROM xp_transactions
                 WHERE created_at >= date_trunc('day', NOW() AT TIME ZONE 'utc')) AS XpToday,
                (SELECT COALESCE(SUM(CASE WHEN xp_amount > 0 THEN xp_amount ELSE 0 END), 0)
                 FROM xp_transactions
                 WHERE created_at >= date_trunc('week', NOW() AT TIME ZONE 'utc')) AS XpThisWeek,
                (SELECT COALESCE(AVG(level), 0) FROM user_progress WHERE total_xp > 0) AS AverageLevel
            """,
            cancellationToken: cancellationToken));

        var levelDist = (await connection.QueryAsync<AnalyticsNamedCountLite>(new CommandDefinition(
            """
            SELECT level::text AS Name, COUNT(*)::bigint AS Count
            FROM user_progress
            WHERE total_xp > 0
            GROUP BY level
            ORDER BY level
            LIMIT 50
            """,
            cancellationToken: cancellationToken))).ToList();

        var xpByRule = (await connection.QueryAsync<AnalyticsNamedCountLite>(new CommandDefinition(
            """
            SELECT rule_code AS Name, COALESCE(SUM(CASE WHEN xp_amount > 0 THEN xp_amount ELSE 0 END), 0)::bigint AS Count
            FROM xp_transactions
            GROUP BY rule_code
            ORDER BY Count DESC
            LIMIT 20
            """,
            cancellationToken: cancellationToken))).ToList();

        return new GamificationOverviewResponse(
            (long)totals.TotalUsers,
            (long)totals.UsersWithXp,
            (long)totals.TotalXpAwarded,
            (long)totals.XpToday,
            (long)totals.XpThisWeek,
            Math.Round((double)totals.AverageLevel, 2),
            levelDist,
            xpByRule);
    }

    public async Task<(IReadOnlyList<AdminUserListItem> Items, int Total)> ListUsersAsync(
        string? search,
        int? level,
        string? status,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;
        var searchLike = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

        var orderBy = sort?.ToLowerInvariant() switch
        {
            "xp" or "totalxp" => "COALESCE(p.total_xp, 0) DESC",
            "level" => "COALESCE(p.level, 1) DESC",
            "created" => "u.created_at DESC",
            "activity" => "p.last_activity_at DESC NULLS LAST",
            _ => "u.created_at DESC"
        };

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM users u
            LEFT JOIN user_progress p ON p.user_id = u.id
            WHERE (@Search IS NULL OR u.email ILIKE @Search OR u.display_name ILIKE @Search OR u.username ILIKE @Search)
              AND (@Level IS NULL OR COALESCE(p.level, 1) = @Level)
              AND (@Status IS NULL OR u.status = @Status)
            """,
            new { Search = searchLike, Level = level, Status = status },
            cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<AdminUserListItem>(new CommandDefinition(
            $"""
            SELECT
                u.id AS Id,
                u.email AS Email,
                COALESCE(NULLIF(u.display_name, ''), u.username, split_part(u.email, '@', 1)) AS DisplayName,
                u.status AS Status,
                COALESCE(p.level, 1) AS Level,
                COALESCE(p.total_xp, 0) AS TotalXp,
                COALESCE((
                    SELECT COUNT(DISTINCT game_id)::int FROM game_play_sessions
                    WHERE user_id = u.id AND is_valid = TRUE
                ), 0) AS UniqueGamesPlayed,
                COALESCE((SELECT COUNT(*)::int FROM user_favorites WHERE user_id = u.id), 0) AS Favorites,
                COALESCE(p.current_streak, 0) AS CurrentStreak,
                p.last_activity_at AS LastActivityAt,
                u.created_at AS CreatedAt
            FROM users u
            LEFT JOIN user_progress p ON p.user_id = u.id
            WHERE (@Search IS NULL OR u.email ILIKE @Search OR u.display_name ILIKE @Search OR u.username ILIKE @Search)
              AND (@Level IS NULL OR COALESCE(p.level, 1) = @Level)
              AND (@Status IS NULL OR u.status = @Status)
            ORDER BY {orderBy}
            LIMIT @Limit OFFSET @Offset
            """,
            new { Search = searchLike, Level = level, Status = status, Limit = pageSize, Offset = offset },
            cancellationToken: cancellationToken))).ToList();

        return (items, total);
    }

    public async Task<AdminUserDetailResponse> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var user = await connection.QuerySingleOrDefaultAsync<AdminUserListItem>(new CommandDefinition(
            """
            SELECT
                u.id AS Id,
                u.email AS Email,
                COALESCE(NULLIF(u.display_name, ''), u.username, split_part(u.email, '@', 1)) AS DisplayName,
                u.status AS Status,
                COALESCE(p.level, 1) AS Level,
                COALESCE(p.total_xp, 0) AS TotalXp,
                COALESCE((
                    SELECT COUNT(DISTINCT game_id)::int FROM game_play_sessions
                    WHERE user_id = u.id AND is_valid = TRUE
                ), 0) AS UniqueGamesPlayed,
                COALESCE((SELECT COUNT(*)::int FROM user_favorites WHERE user_id = u.id), 0) AS Favorites,
                COALESCE(p.current_streak, 0) AS CurrentStreak,
                p.last_activity_at AS LastActivityAt,
                u.created_at AS CreatedAt
            FROM users u
            LEFT JOIN user_progress p ON p.user_id = u.id
            WHERE u.id = @UserId
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        if (user is null)
        {
            throw new NotFoundException("User", "User not found.");
        }

        var progress = await xpStore.GetOrCreateProgressAsync(userId, cancellationToken);
        var level = await levelService.CalculateAsync(progress.TotalXp, cancellationToken);
        var recent = await xpStore.GetTransactionsAsync(userId, 20, 0, null, null, null, cancellationToken);
        var totalSessions = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*)::int FROM game_play_sessions WHERE user_id = @UserId AND is_valid = TRUE",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        var stats = new ProgressStatsDto(
            totalSessions,
            user.UniqueGamesPlayed,
            user.Favorites,
            progress.CurrentStreak,
            progress.LongestStreak);

        return new AdminUserDetailResponse(user, level, stats, recent);
    }

    public async Task<XpAwardResult> AdjustXpAsync(
        Guid userId,
        Guid adminId,
        int amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("Reason is required.");
        }

        if (amount == 0)
        {
            throw new ValidationException("Amount must not be zero.");
        }

        var result = await xpEngine.AdminAdjustAsync(userId, adminId, amount, reason.Trim(), cancellationToken);

        await auditLog.WriteAsync(
            adminId,
            AuditActions.AdminXpAdjustment,
            "User",
            userId.ToString("D"),
            before: null,
            after: new { amount = result.XpAwarded, totalXp = result.TotalXp, leveledUp = result.LeveledUp },
            reason: reason.Trim(),
            cancellationToken: cancellationToken);

        return result;
    }

    public async Task ResetGamificationAsync(
        Guid userId,
        Guid adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("Reason is required.");
        }

        var progress = await xpStore.GetOrCreateProgressAsync(userId, cancellationToken);
        var before = new { progress.TotalXp, progress.Level, progress.CurrentLevelXp, progress.CurrentStreak, progress.LongestStreak };

        if (progress.TotalXp > 0)
        {
            await xpEngine.AdminAdjustAsync(
                userId,
                adminId,
                -(int)Math.Min(progress.TotalXp, int.MaxValue),
                $"Gamification reset: {reason.Trim()}",
                cancellationToken);
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_progress
            SET current_streak = 0,
                longest_streak = longest_streak,
                games_played = 0,
                unique_games_played = 0,
                updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE user_id = @UserId
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        var afterProgress = await xpStore.GetOrCreateProgressAsync(userId, cancellationToken);
        await auditLog.WriteAsync(
            adminId,
            AuditActions.AdminGamificationReset,
            "User",
            userId.ToString("D"),
            before,
            new { afterProgress.TotalXp, afterProgress.Level, afterProgress.CurrentLevelXp },
            reason.Trim(),
            cancellationToken: cancellationToken);
    }

    public async Task ResetStreakAsync(
        Guid userId,
        Guid adminId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var before = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT current_streak AS CurrentStreak, longest_streak AS LongestStreak FROM user_progress WHERE user_id = @UserId",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_progress (user_id, total_xp, level, current_level_xp, current_streak, longest_streak,
                                       streak_status, streak_start_date, last_qualifying_activity_date,
                                       created_at, updated_at)
            VALUES (@UserId, 0, 1, 0, 0, 0, 'BROKEN', NULL, NULL,
                    (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            ON CONFLICT (user_id) DO UPDATE SET
                current_streak = 0,
                streak_status = 'BROKEN',
                streak_start_date = NULL,
                last_qualifying_activity_date = NULL,
                streak_updated_at = (NOW() AT TIME ZONE 'utc'),
                updated_at = (NOW() AT TIME ZONE 'utc');
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        await auditLog.WriteAsync(
            adminId,
            AuditActions.AdminStreakReset,
            "User",
            userId.ToString("D"),
            before,
            new { CurrentStreak = 0 },
            reason,
            cancellationToken: cancellationToken);
    }

    public async Task SetUserStatusAsync(
        Guid userId,
        Guid adminId,
        string status,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        status = status.Trim().ToLowerInvariant();
        if (status is not ("active" or "suspended"))
        {
            throw new ValidationException("Status must be active or suspended.");
        }

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var before = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT status FROM users WHERE id = @UserId",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        if (before is null)
        {
            throw new NotFoundException("User", "User not found.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE users SET status = @Status, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE id = @UserId
            """,
            new { UserId = userId, Status = status },
            cancellationToken: cancellationToken));

        await auditLog.WriteAsync(
            adminId,
            status == "suspended" ? AuditActions.AdminUserSuspended : AuditActions.AdminUserUnsuspended,
            "User",
            userId.ToString("D"),
            new { status = before },
            new { status },
            reason,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<LevelDefinitionDto>> ListLevelsAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LevelDefinitionDto>(new CommandDefinition(
            """
            SELECT level AS Level, required_total_xp AS RequiredTotalXp, title AS Title,
                   description AS Description, is_active AS IsActive
            FROM gamification_levels
            WHERE (@IncludeInactive OR is_active = TRUE)
            ORDER BY level
            """,
            new { IncludeInactive = includeInactive },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<LevelDefinitionDto> CreateLevelAsync(
        Guid adminId,
        UpsertLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("Title is required.");
        }

        await ValidateLevelCurveAsync(request.Level, request.RequiredTotalXp, excludeLevel: null, cancellationToken);

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO gamification_levels (id, level, required_total_xp, title, description, is_active, created_at, updated_at)
            VALUES (@Id, @Level, @RequiredTotalXp, @Title, @Description, @IsActive,
                    (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                request.Level,
                request.RequiredTotalXp,
                Title = request.Title.Trim(),
                Description = request.Description,
                request.IsActive
            },
            cancellationToken: cancellationToken));

        await levelService.RefreshCacheAsync(cancellationToken);
        var created = new LevelDefinitionDto(request.Level, request.RequiredTotalXp, request.Title.Trim(), request.Description, request.IsActive);
        await auditLog.WriteAsync(
            adminId,
            AuditActions.AdminLevelCreated,
            "GamificationLevel",
            request.Level.ToString(),
            null,
            created,
            null,
            cancellationToken: cancellationToken);
        return created;
    }

    public async Task<LevelDefinitionDto> UpdateLevelAsync(
        Guid adminId,
        int level,
        UpsertLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        if (level != request.Level)
        {
            throw new ValidationException("Path level must match body level.");
        }

        await ValidateLevelCurveAsync(request.Level, request.RequiredTotalXp, excludeLevel: level, cancellationToken);

        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var before = await connection.QuerySingleOrDefaultAsync<LevelDefinitionDto>(new CommandDefinition(
            """
            SELECT level AS Level, required_total_xp AS RequiredTotalXp, title AS Title,
                   description AS Description, is_active AS IsActive
            FROM gamification_levels WHERE level = @Level
            """,
            new { Level = level },
            cancellationToken: cancellationToken));

        if (before is null)
        {
            throw new NotFoundException("Level", "Level not found.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE gamification_levels
            SET required_total_xp = @RequiredTotalXp,
                title = @Title,
                description = @Description,
                is_active = @IsActive,
                updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE level = @Level
            """,
            new
            {
                Level = level,
                request.RequiredTotalXp,
                Title = request.Title.Trim(),
                Description = request.Description,
                request.IsActive
            },
            cancellationToken: cancellationToken));

        await levelService.RefreshCacheAsync(cancellationToken);
        var after = new LevelDefinitionDto(level, request.RequiredTotalXp, request.Title.Trim(), request.Description, request.IsActive);
        await auditLog.WriteAsync(
            adminId,
            AuditActions.AdminLevelUpdated,
            "GamificationLevel",
            level.ToString(),
            before,
            after,
            null,
            cancellationToken: cancellationToken);
        return after;
    }

    public async Task SetLevelActiveAsync(
        Guid adminId,
        int level,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE gamification_levels
            SET is_active = @IsActive, updated_at = (NOW() AT TIME ZONE 'utc')
            WHERE level = @Level
            """,
            new { Level = level, IsActive = isActive },
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            throw new NotFoundException("Level", "Level not found.");
        }

        await levelService.RefreshCacheAsync(cancellationToken);
        await auditLog.WriteAsync(
            adminId,
            isActive ? AuditActions.AdminLevelActivated : AuditActions.AdminLevelDeactivated,
            "GamificationLevel",
            level.ToString(),
            null,
            new { level, isActive },
            null,
            cancellationToken: cancellationToken);
    }

    private async Task ValidateLevelCurveAsync(
        int level,
        long requiredTotalXp,
        int? excludeLevel,
        CancellationToken cancellationToken)
    {
        if (level < 1 || level > 1000)
        {
            throw new ValidationException("Level must be between 1 and 1000.");
        }

        if (requiredTotalXp < 0)
        {
            throw new ValidationException("RequiredTotalXp must be >= 0.");
        }

        var levels = (await ListLevelsAsync(includeInactive: true, cancellationToken)).ToList();
        if (excludeLevel is null && levels.Any(l => l.Level == level))
        {
            throw new ValidationException("Duplicate level number.");
        }

        if (levels.Any(l => l.Level != excludeLevel && l.RequiredTotalXp == requiredTotalXp))
        {
            throw new ValidationException("Duplicate RequiredTotalXp.");
        }

        var projected = levels
            .Where(l => l.Level != excludeLevel)
            .Append(new LevelDefinitionDto(level, requiredTotalXp, "x", null, true))
            .OrderBy(l => l.Level)
            .ToList();

        for (var i = 1; i < projected.Count; i++)
        {
            if (projected[i].RequiredTotalXp <= projected[i - 1].RequiredTotalXp)
            {
                throw new ValidationException("RequiredTotalXp must strictly increase with level.");
            }
        }
    }
}

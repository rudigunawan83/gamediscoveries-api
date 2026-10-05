using System.Data.Common;
using System.Text.Json;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Xp.Models;

namespace GameDiscoveries.Modules.Xp.Services;

public interface IAuditLogService
{
    Task WriteAsync(
        Guid? adminId,
        string action,
        string targetType,
        string targetId,
        object? before,
        object? after,
        string? reason,
        string? ipHash = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDto>> ListAsync(
        int limit,
        int offset,
        string? action,
        string? targetType,
        Guid? adminId,
        CancellationToken cancellationToken = default);
}

public static class AuditActions
{
    public const string AdminXpAdjustment = "ADMIN_XP_ADJUSTMENT";
    public const string AdminGamificationReset = "ADMIN_GAMIFICATION_RESET";
    public const string AdminStreakReset = "ADMIN_STREAK_RESET";
    public const string AdminUserSuspended = "ADMIN_USER_SUSPENDED";
    public const string AdminUserUnsuspended = "ADMIN_USER_UNSUSPENDED";
    public const string AdminLevelCreated = "ADMIN_LEVEL_CREATED";
    public const string AdminLevelUpdated = "ADMIN_LEVEL_UPDATED";
    public const string AdminLevelActivated = "ADMIN_LEVEL_ACTIVATED";
    public const string AdminLevelDeactivated = "ADMIN_LEVEL_DEACTIVATED";
    public const string AdminAchievementCreated = "ADMIN_ACHIEVEMENT_CREATED";
    public const string AdminAchievementUpdated = "ADMIN_ACHIEVEMENT_UPDATED";
    public const string AdminAchievementActivated = "ADMIN_ACHIEVEMENT_ACTIVATED";
    public const string AdminAchievementDeactivated = "ADMIN_ACHIEVEMENT_DEACTIVATED";
    public const string AdminAchievementGranted = "ADMIN_ACHIEVEMENT_GRANTED";
    public const string AdminAchievementRevoked = "ADMIN_ACHIEVEMENT_REVOKED";
    public const string AdminDiscoveryConfigUpdated = "ADMIN_DISCOVERY_CONFIG_UPDATED";
    public const string AdminLeaderboardRebuild = "ADMIN_LEADERBOARD_REBUILD";
    public const string AdminLeaderboardUserDisqualified = "ADMIN_LEADERBOARD_USER_DISQUALIFIED";
    public const string AdminLeaderboardUserRestored = "ADMIN_LEADERBOARD_USER_RESTORED";
    public const string AdminCompetitionSettled = "ADMIN_COMPETITION_SETTLED";
}

public sealed class AuditLogService(IDbConnectionFactory connectionFactory) : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(
        Guid? adminId,
        string action,
        string targetType,
        string targetId,
        object? before,
        object? after,
        string? reason,
        string? ipHash = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_logs (id, admin_id, action, target_type, target_id, before_json, after_json, reason, ip_hash, created_at)
            VALUES (@Id, @AdminId, @Action, @TargetType, @TargetId,
                    CAST(@BeforeJson AS jsonb), CAST(@AfterJson AS jsonb),
                    @Reason, @IpHash, (NOW() AT TIME ZONE 'utc'))
            """,
            new
            {
                Id = Guid.NewGuid(),
                AdminId = adminId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
                AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
                Reason = reason,
                IpHash = ipHash
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(
        int limit,
        int offset,
        string? action,
        string? targetType,
        Guid? adminId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuditLogDto>(new CommandDefinition(
            """
            SELECT id AS Id, admin_id AS AdminId, action AS Action,
                   target_type AS TargetType, target_id AS TargetId,
                   reason AS Reason, created_at AS CreatedAt
            FROM audit_logs
            WHERE (@Action IS NULL OR action = @Action)
              AND (@TargetType IS NULL OR target_type = @TargetType)
              AND (@AdminId IS NULL OR admin_id = @AdminId)
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset
            """,
            new
            {
                Action = action,
                TargetType = targetType,
                AdminId = adminId,
                Limit = Math.Clamp(limit, 1, 200),
                Offset = Math.Max(0, offset)
            },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }
}

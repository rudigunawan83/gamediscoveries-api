using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Xp.Data;
using GameDiscoveries.Modules.Xp.Models;

namespace GameDiscoveries.Modules.Xp.Services;

public interface IProgressService
{
    Task<UserProgressResponse> GetMyProgressAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedXpTransactionsResponse> GetMyXpTransactionsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? ruleCode,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default);
}

public sealed class ProgressService(
    IDbConnectionFactory connectionFactory,
    IXpStore xpStore,
    ILevelService levelService,
    IEnumerable<IStreakProgressProvider> streakProviders) : IProgressService
{
    public async Task<UserProgressResponse> GetMyProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);

        var user = await connection.QuerySingleOrDefaultAsync<ProgressUserRow>(new CommandDefinition(
            """
            SELECT id AS Id, COALESCE(NULLIF(display_name, ''), username, split_part(email, '@', 1)) AS DisplayName,
                   avatar_url AS AvatarUrl
            FROM users
            WHERE id = @UserId
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        if (user is null)
        {
            throw new NotFoundException("User", "User not found.");
        }

        var progress = await xpStore.GetOrCreateProgressAsync(userId, cancellationToken);
        var level = await levelService.CalculateAsync(progress.TotalXp, cancellationToken);

        if (progress.Level != level.Level || progress.CurrentLevelXp != level.CurrentLevelXp)
        {
            await xpStore.ApplyLevelAsync(userId, level.Level, level.CurrentLevelXp, cancellationToken);
        }

        var stats = await LoadStatsAsync(connection, userId, progress, cancellationToken);

        ProgressStreakDto? streakDto = null;
        var provider = streakProviders.FirstOrDefault();
        if (provider is not null)
        {
            var snap = await provider.GetSnapshotAsync(userId, cancellationToken);
            streakDto = new ProgressStreakDto(
                snap.Current,
                snap.Longest,
                snap.Status,
                snap.TodayQualified,
                snap.FreezeCount,
                snap.NextMilestoneDays);
            stats = stats with
            {
                CurrentStreak = snap.Current,
                LongestStreak = snap.Longest
            };
        }

        return new UserProgressResponse(
            new ProgressUserDto(user.Id, user.DisplayName, user.AvatarUrl),
            level,
            stats,
            streakDto);
    }

    public async Task<PagedXpTransactionsResponse> GetMyXpTransactionsAsync(
        Guid userId,
        int page,
        int pageSize,
        string? ruleCode,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var total = await xpStore.CountTransactionsAsync(userId, ruleCode, dateFrom, dateTo, cancellationToken);
        var items = await xpStore.GetTransactionsAsync(userId, pageSize, offset, ruleCode, dateFrom, dateTo, cancellationToken);

        return new PagedXpTransactionsResponse(items, page, pageSize, total);
    }

    private static async Task<ProgressStatsDto> LoadStatsAsync(
        DbConnection connection,
        Guid userId,
        UserProgressEntity progress,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<ProgressStatsRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*)::int FROM game_play_sessions
                 WHERE user_id = @UserId AND is_valid = TRUE) AS TotalGameSessions,
                (SELECT COUNT(DISTINCT game_id)::int FROM game_play_sessions
                 WHERE user_id = @UserId AND is_valid = TRUE) AS UniqueGamesPlayed,
                (SELECT COUNT(*)::int FROM user_favorites WHERE user_id = @UserId) AS Favorites
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return new ProgressStatsDto(
            row.TotalGameSessions,
            row.UniqueGamesPlayed,
            row.Favorites,
            progress.CurrentStreak,
            progress.LongestStreak);
    }

    private sealed class ProgressUserRow
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    private sealed class ProgressStatsRow
    {
        public int TotalGameSessions { get; set; }
        public int UniqueGamesPlayed { get; set; }
        public int Favorites { get; set; }
    }
}

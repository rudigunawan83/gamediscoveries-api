using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Database;
using GameDiscoveries.Modules.Xp.Models;

namespace GameDiscoveries.Modules.Xp.Services;

public interface ILevelService
{
    Task<IReadOnlyList<LevelDefinitionDto>> GetActiveLevelsAsync(CancellationToken cancellationToken = default);

    Task<LevelInfo> CalculateAsync(long totalXp, CancellationToken cancellationToken = default);

    Task RefreshCacheAsync(CancellationToken cancellationToken = default);
}

public sealed class LevelService(IDbConnectionFactory connectionFactory) : ILevelService
{
    private volatile IReadOnlyList<LevelDefinitionDto> _cache = Array.Empty<LevelDefinitionDto>();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<LevelDefinitionDto>> GetActiveLevelsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.Count == 0)
        {
            await RefreshCacheAsync(cancellationToken);
        }

        return _cache;
    }

    public async Task<LevelInfo> CalculateAsync(long totalXp, CancellationToken cancellationToken = default)
    {
        var levels = await GetActiveLevelsAsync(cancellationToken);
        if (levels.Count == 0)
        {
            return new LevelInfo(1, "Newcomer", null, totalXp, totalXp, 0, 100, true);
        }

        var ordered = levels.OrderBy(l => l.RequiredTotalXp).ToList();
        var current = ordered[0];
        foreach (var level in ordered)
        {
            if (level.RequiredTotalXp <= totalXp)
            {
                current = level;
            }
            else
            {
                break;
            }
        }

        var next = ordered.FirstOrDefault(l => l.RequiredTotalXp > current.RequiredTotalXp);
        var isMax = next is null;
        var currentLevelXp = Math.Max(0, totalXp - current.RequiredTotalXp);
        var nextLevelXp = isMax ? 0 : Math.Max(0, next!.RequiredTotalXp - current.RequiredTotalXp);
        var progress = isMax
            ? 100d
            : nextLevelXp == 0
                ? 100d
                : Math.Clamp(100d * currentLevelXp / nextLevelXp, 0, 100);

        return new LevelInfo(
            current.Level,
            current.Title,
            current.Description,
            totalXp,
            currentLevelXp,
            nextLevelXp,
            Math.Round(progress, 2),
            isMax,
            next?.Level,
            next?.Title);
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
            var rows = (await connection.QueryAsync<LevelDefinitionDto>(new CommandDefinition(
                """
                SELECT level AS Level, required_total_xp AS RequiredTotalXp, title AS Title,
                       description AS Description, is_active AS IsActive
                FROM gamification_levels
                WHERE is_active = TRUE
                ORDER BY level
                """,
                cancellationToken: cancellationToken))).ToList();
            _cache = rows;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>
/// Pure calculator for unit tests without DB.
/// </summary>
public static class LevelCalculator
{
    public static LevelInfo Calculate(long totalXp, IReadOnlyList<LevelDefinitionDto> levels)
    {
        if (levels.Count == 0)
        {
            return new LevelInfo(1, "Newcomer", null, totalXp, totalXp, 0, 100, true);
        }

        var ordered = levels.Where(l => l.IsActive).OrderBy(l => l.RequiredTotalXp).ToList();
        if (ordered.Count == 0)
        {
            return new LevelInfo(1, "Newcomer", null, totalXp, totalXp, 0, 100, true);
        }

        var current = ordered[0];
        foreach (var level in ordered)
        {
            if (level.RequiredTotalXp <= totalXp)
            {
                current = level;
            }
            else break;
        }

        var next = ordered.FirstOrDefault(l => l.RequiredTotalXp > current.RequiredTotalXp);
        var isMax = next is null;
        var currentLevelXp = Math.Max(0, totalXp - current.RequiredTotalXp);
        var nextLevelXp = isMax ? 0 : Math.Max(0, next!.RequiredTotalXp - current.RequiredTotalXp);
        var progress = isMax || nextLevelXp == 0
            ? 100d
            : Math.Clamp(100d * currentLevelXp / nextLevelXp, 0, 100);

        return new LevelInfo(
            current.Level,
            current.Title,
            current.Description,
            totalXp,
            currentLevelXp,
            nextLevelXp,
            Math.Round(progress, 2),
            isMax,
            next?.Level,
            next?.Title);
    }
}

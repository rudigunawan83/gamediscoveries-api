using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.Modules.Leaderboards.Data;
using GameDiscoveries.Modules.Leaderboards.Domain;
using GameDiscoveries.Modules.Leaderboards.Options;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Leaderboards.Services;

public interface ILeaderboardService
{
    Task<IReadOnlyList<LeaderboardListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<LeaderboardDetailResponse> GetAsync(string code, int? limit, Guid? userId, CancellationToken cancellationToken = default);
    Task<UserRankResponse> GetMeAsync(string code, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetMyHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetHistoryAsync(string code, int limit, CancellationToken cancellationToken = default);
    Task EnsureActivePeriodsAsync(CancellationToken cancellationToken = default);
    Task ReconcileActiveAsync(CancellationToken cancellationToken = default);
    Task SnapshotActiveAsync(CancellationToken cancellationToken = default);
    Task RebuildAsync(string code, Guid adminId, string reason, CancellationToken cancellationToken = default);
    Task DisqualifyAsync(string code, Guid userId, bool disqualified, Guid adminId, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminLeaderboardOverviewDto>> AdminOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionDto>> ListCompetitionsAsync(CancellationToken cancellationToken = default);
    Task JoinCompetitionAsync(string code, Guid userId, CancellationToken cancellationToken = default);
    Task SettleCompetitionAsync(string code, Guid adminId, string reason, CancellationToken cancellationToken = default);
}

public sealed class LeaderboardService(
    ILeaderboardStore store,
    ILeaderboardEligibilityService eligibility,
    ICacheService cache,
    IServiceScopeFactory scopeFactory,
    IAuditLogService auditLog,
    IOptions<LeaderboardOptions> optionsAccessor,
    ILogger<LeaderboardService> logger) : ILeaderboardService, ILeaderboardXpSink
{
    public async Task OnXpTransactionAsync(
        Guid userId,
        string ruleCode,
        string eventType,
        int xpAmount,
        DateTimeOffset createdAt,
        string referenceType,
        string referenceId,
        CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        if (!options.Enabled) return;
        if (!eligibility.IsXpTransactionEligible(ruleCode, eventType, xpAmount)) return;

        try
        {
            await EnsureActivePeriodsAsync(cancellationToken);
            var definitions = await store.ListDefinitionsAsync(cancellationToken);
            var countSession = string.Equals(eventType, "GAME_SESSION_END", StringComparison.OrdinalIgnoreCase)
                               && xpAmount > 0;

            foreach (var definition in definitions.Where(d => d.ScopeType == "GLOBAL"))
            {
                var period = await store.GetActivePeriodAsync(definition.Id, cancellationToken);
                if (period is null) continue;
                if (createdAt < period.StartAt || createdAt >= period.EndAt) continue;

                await store.UpsertScoreDeltaAsync(
                    definition.Id, period.Id, userId, xpAmount, countSession, createdAt, cancellationToken);
                await store.RecalculateRanksAsync(definition.Id, period.Id, cancellationToken);
                await InvalidateCacheAsync(definition.Code, userId, cancellationToken);
            }

            logger.LogInformation(
                "LeaderboardScoreUpdated user={UserId} rule={Rule} xp={Xp} ref={RefType}:{RefId}",
                userId, ruleCode, xpAmount, referenceType, referenceId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "LeaderboardScoreUpdateFailed user={UserId}", userId);
        }
    }

    public async Task EnsureActivePeriodsAsync(CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        var now = DateTimeOffset.UtcNow;
        await store.EndExpiredPeriodsAsync(now, cancellationToken);

        var definitions = await store.ListDefinitionsAsync(cancellationToken);
        foreach (var definition in definitions)
        {
            var (start, end, code) = definition.PeriodType switch
            {
                "WEEKLY" => LeaderboardPeriodCalculator.CurrentWeekly(now, options.Timezone),
                "MONTHLY" => LeaderboardPeriodCalculator.CurrentMonthly(now, options.Timezone),
                "ALL_TIME" => LeaderboardPeriodCalculator.AllTime(options.Timezone),
                _ => LeaderboardPeriodCalculator.CurrentWeekly(now, options.Timezone)
            };

            await store.EnsurePeriodAsync(definition, start, end, code, options.Timezone, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<LeaderboardListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureActivePeriodsAsync(cancellationToken);
        var definitions = await store.ListDefinitionsAsync(cancellationToken);
        var result = new List<LeaderboardListItemDto>();
        foreach (var d in definitions)
        {
            var period = await store.GetActivePeriodAsync(d.Id, cancellationToken);
            var participants = period is null ? 0 : await store.CountParticipantsAsync(d.Id, period.Id, cancellationToken);
            result.Add(new LeaderboardListItemDto(
                d.Code,
                d.Name,
                d.Type,
                d.Description,
                period is null ? null : ToPeriodDto(period),
                participants));
        }

        return result;
    }

    public async Task<LeaderboardDetailResponse> GetAsync(
        string code,
        int? limit,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        var take = Math.Clamp(limit ?? options.DefaultLimit, 1, options.TopMaxLimit);
        var cacheKey = $"leaderboard:top:{Normalize(code)}:{take}";

        LeaderboardDetailResponse? cached = null;
        try
        {
            cached = await cache.GetAsync<LeaderboardDetailResponse>(cacheKey, cancellationToken);
        }
        catch
        {
            // Redis unavailable → PostgreSQL
        }

        if (cached is not null && userId is null)
        {
            return cached;
        }

        await EnsureActivePeriodsAsync(cancellationToken);
        var definition = await store.GetDefinitionByCodeAsync(code, cancellationToken)
                         ?? throw new KeyNotFoundException("Leaderboard not found.");
        var period = await store.GetActivePeriodAsync(definition.Id, cancellationToken)
                     ?? throw new InvalidOperationException("LEADERBOARD_UNAVAILABLE");

        var top = await store.GetTopAsync(definition.Id, period.Id, take, cancellationToken);
        var participants = await store.CountParticipantsAsync(definition.Id, period.Id, cancellationToken);
        var items = top.Select(MapItem).ToList();

        LeaderboardItemDto? me = null;
        if (userId is Guid uid)
        {
            var entry = await store.GetUserEntryAsync(definition.Id, period.Id, uid, cancellationToken);
            if (entry is not null && entry.Score > 0 && !entry.IsDisqualified)
            {
                me = MapItem(entry);
            }
        }

        var response = new LeaderboardDetailResponse(
            new LeaderboardMetaDto(
                definition.Code,
                definition.Name,
                definition.Description,
                definition.Type,
                definition.ScoreType,
                options.Version,
                ToPeriodDto(period)),
            items,
            me,
            participants);

        try
        {
            await cache.SetAsync(cacheKey, response with { Me = null }, TimeSpan.FromSeconds(options.TopCacheSeconds), cancellationToken);
        }
        catch
        {
            // ignore cache write failures
        }

        return response;
    }

    public async Task<UserRankResponse> GetMeAsync(string code, Guid userId, CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        await EnsureActivePeriodsAsync(cancellationToken);
        var definition = await store.GetDefinitionByCodeAsync(code, cancellationToken)
                         ?? throw new KeyNotFoundException("Leaderboard not found.");
        var period = await store.GetActivePeriodAsync(definition.Id, cancellationToken);
        if (period is null)
        {
            return new UserRankResponse(null, 0, null, null, null, null, 0, 0, 0, null, null, null);
        }

        var entry = await store.GetUserEntryAsync(definition.Id, period.Id, userId, cancellationToken);
        if (entry is null || entry.Score <= 0 || entry.IsDisqualified)
        {
            return new UserRankResponse(null, 0, null, null, null, null, 0, 0, 0, null, null, ToPeriodDto(period));
        }

        var participants = await store.CountParticipantsAsync(definition.Id, period.Id, cancellationToken);
        var (nextRank, nextScore) = await store.GetNextRankTargetAsync(definition.Id, period.Id, entry.Score, entry.Rank, cancellationToken);
        long? xpNeeded = nextScore is null ? null : Math.Max(0, nextScore.Value - entry.Score + 1);

        return new UserRankResponse(
            entry.Rank,
            entry.Score,
            entry.PreviousRank,
            entry.PreviousRank is null ? null : entry.RankChange,
            LeaderboardRankHelper.ToMovement(entry.PreviousRank, entry.Rank, entry.RankChange),
            LeaderboardRankHelper.Percentile(entry.Rank, participants),
            entry.GamesPlayed,
            entry.ValidSessions,
            entry.XpEarned,
            nextRank,
            xpNeeded,
            ToPeriodDto(period));
    }

    public Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetMyHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
        => store.GetUserHistoryAsync(userId, Math.Clamp(limit, 1, 50), cancellationToken);

    public async Task<IReadOnlyList<LeaderboardHistoryItemDto>> GetHistoryAsync(string code, int limit, CancellationToken cancellationToken = default)
    {
        var definition = await store.GetDefinitionByCodeAsync(code, cancellationToken)
                         ?? throw new KeyNotFoundException("Leaderboard not found.");
        return await store.GetPeriodHistoryAsync(definition.Id, Math.Clamp(limit, 1, 50), cancellationToken);
    }

    public async Task ReconcileActiveAsync(CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        await EnsureActivePeriodsAsync(cancellationToken);
        var definitions = await store.ListDefinitionsAsync(cancellationToken);
        foreach (var definition in definitions)
        {
            var period = await store.GetActivePeriodAsync(definition.Id, cancellationToken);
            if (period is null) continue;
            await store.RebuildPeriodFromXpAsync(
                definition.Id,
                period.Id,
                period.StartAt,
                period.EndAt,
                options.IncludeAdminAdjustmentInLeaderboard,
                cancellationToken);
            await InvalidateCacheAsync(definition.Code, null, cancellationToken);
            logger.LogInformation("LeaderboardReconciled code={Code} period={Period}", definition.Code, period.Code);
        }
    }

    public async Task SnapshotActiveAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await store.ListDefinitionsAsync(cancellationToken);
        foreach (var definition in definitions)
        {
            var period = await store.GetActivePeriodAsync(definition.Id, cancellationToken);
            if (period is null) continue;
            await store.CreateSnapshotAsync(definition.Id, period.Id, cancellationToken);
        }
    }

    public async Task RebuildAsync(string code, Guid adminId, string reason, CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        var definition = await store.GetDefinitionByCodeAsync(code, cancellationToken)
                         ?? throw new KeyNotFoundException("Leaderboard not found.");
        var period = await store.GetActivePeriodAsync(definition.Id, cancellationToken)
                     ?? throw new InvalidOperationException("No active period.");
        await store.RebuildPeriodFromXpAsync(
            definition.Id, period.Id, period.StartAt, period.EndAt,
            options.IncludeAdminAdjustmentInLeaderboard, cancellationToken);
        await InvalidateCacheAsync(definition.Code, null, cancellationToken);
        await auditLog.WriteAsync(
            adminId,
            "ADMIN_LEADERBOARD_REBUILD",
            "Leaderboard",
            definition.Code,
            null,
            new { period.Code },
            reason,
            cancellationToken: cancellationToken);
    }

    public async Task DisqualifyAsync(
        string code,
        Guid userId,
        bool disqualified,
        Guid adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var definition = await store.GetDefinitionByCodeAsync(code, cancellationToken)
                         ?? throw new KeyNotFoundException("Leaderboard not found.");
        var period = await store.GetActivePeriodAsync(definition.Id, cancellationToken)
                     ?? throw new InvalidOperationException("No active period.");
        await store.SetDisqualifiedAsync(definition.Id, period.Id, userId, disqualified, cancellationToken);
        await InvalidateCacheAsync(definition.Code, userId, cancellationToken);
        await auditLog.WriteAsync(
            adminId,
            disqualified ? "ADMIN_LEADERBOARD_USER_DISQUALIFIED" : "ADMIN_LEADERBOARD_USER_RESTORED",
            "User",
            userId.ToString(),
            null,
            new { LeaderboardCode = definition.Code, PeriodCode = period.Code, Disqualified = disqualified },
            reason,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<AdminLeaderboardOverviewDto>> AdminOverviewAsync(CancellationToken cancellationToken = default)
        => store.AdminOverviewAsync(cancellationToken);

    public Task<IReadOnlyList<CompetitionDto>> ListCompetitionsAsync(CancellationToken cancellationToken = default)
        => store.ListCompetitionsAsync(cancellationToken);

    public Task JoinCompetitionAsync(string code, Guid userId, CancellationToken cancellationToken = default)
        => store.JoinCompetitionAsync(code, userId, cancellationToken);

    public async Task SettleCompetitionAsync(string code, Guid adminId, string reason, CancellationToken cancellationToken = default)
    {
        var (awarded, skipped) = await store.SettleCompetitionAsync(code, async (userId, xp) =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var xpEngine = scope.ServiceProvider.GetRequiredService<IXpEngine>();
            var result = await xpEngine.AwardAsync(new XpAwardRequest(
                userId,
                "COMPETITION_REWARD",
                "COMPETITION_REWARD",
                "COMPETITION",
                $"{code}:{userId}",
                xp,
                $"Competition reward for {code}",
                new Dictionary<string, object?> { ["competitionCode"] = code },
                adminId), cancellationToken);

            return result.Success && result.XpAwarded != 0
                ? Guid.NewGuid()
                : null;
        }, cancellationToken);

        await auditLog.WriteAsync(
            adminId,
            "ADMIN_COMPETITION_SETTLED",
            "Competition",
            code,
            null,
            new { awarded, skipped },
            reason,
            cancellationToken: cancellationToken);

        logger.LogInformation("CompetitionRewardAwarded code={Code} awarded={Awarded} skipped={Skipped}", code, awarded, skipped);
    }

    private async Task InvalidateCacheAsync(string code, Guid? userId, CancellationToken cancellationToken)
    {
        try
        {
            var normalized = Normalize(code);
            foreach (var n in new[] { 10, 20, 50, 100 })
            {
                await cache.RemoveAsync($"leaderboard:top:{normalized}:{n}", cancellationToken);
            }

            if (userId is Guid uid)
            {
                await cache.RemoveAsync($"leaderboard:me:{normalized}:{uid:N}", cancellationToken);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static LeaderboardItemDto MapItem(LeaderboardEntryRow row) =>
        new(
            row.Rank ?? 0,
            new LeaderboardUserDto(row.UserId, row.DisplayName, row.Username, row.AvatarUrl, row.Level),
            row.Score,
            row.PreviousRank is null ? null : row.RankChange,
            LeaderboardRankHelper.ToMovement(row.PreviousRank, row.Rank, row.RankChange),
            row.GamesPlayed,
            row.ValidSessions,
            row.XpEarned);

    private static LeaderboardPeriodDto ToPeriodDto(LeaderboardPeriod period) =>
        new(period.Id, period.Code, period.StartAt, period.EndAt, period.Status, period.Timezone);

    private static string Normalize(string code) => code.Replace('-', '_').Trim().ToUpperInvariant();
}

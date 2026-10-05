using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Streaks.Data;
using GameDiscoveries.Modules.Streaks.Domain;
using GameDiscoveries.Modules.Streaks.Models;
using GameDiscoveries.Modules.Streaks.Options;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Streaks.Services;

public interface IStreakService
{
    Task<StreakStatusDto> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ProcessQualifyingSessionAsync(Guid userId, string sessionId, CancellationToken cancellationToken = default);

    Task<PagedStreakHistoryDto> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? eventType,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);

    Task GrantFreezeAsync(Guid userId, Guid adminId, int amount, string reason, CancellationToken cancellationToken = default);

    Task RemoveFreezeAsync(Guid userId, Guid adminId, int amount, string reason, CancellationToken cancellationToken = default);

    Task ResetStreakAsync(Guid userId, Guid adminId, string reason, CancellationToken cancellationToken = default);

    Task<AdminStreakOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<AdminUserStreakDto> GetAdminUserStreakAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IStreakRecoveryService
{
    // Foundation only — Phase 06 does not allow user-initiated recovery.
    Task RecoverAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}

public sealed class NoOpStreakRecoveryService : IStreakRecoveryService
{
    public Task RecoverAsync(Guid userId, string reason, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Streak recovery is not available in this phase.");
}

/// <summary>
/// Pure streak transition logic for unit tests.
/// </summary>
public static class StreakEngine
{
    public sealed record TransitionResult(
        int CurrentStreak,
        int LongestStreak,
        DateOnly StreakStartDate,
        DateOnly LastQualifyingActivityDate,
        string Status,
        int FreezeCount,
        string? EventType,
        int? PreviousStreak,
        bool FreezeConsumed,
        bool SameDayNoOp);

    public static TransitionResult ApplyActivity(
        int currentStreak,
        int longestStreak,
        DateOnly? streakStart,
        DateOnly? lastActivity,
        int freezeCount,
        bool freezeEnabled,
        DateOnly activityDate)
    {
        if (lastActivity == activityDate)
        {
            return new TransitionResult(
                currentStreak,
                longestStreak,
                streakStart ?? activityDate,
                activityDate,
                StreakStatuses.Active,
                freezeCount,
                null,
                null,
                false,
                true);
        }

        if (lastActivity is null || currentStreak <= 0)
        {
            return new TransitionResult(
                1,
                Math.Max(longestStreak, 1),
                activityDate,
                activityDate,
                StreakStatuses.Active,
                freezeCount,
                StreakEventTypes.Started,
                currentStreak,
                false,
                false);
        }

        var gap = StreakDateCalculator.DayGap(lastActivity.Value, activityDate);

        if (gap == 1)
        {
            var next = currentStreak + 1;
            return new TransitionResult(
                next,
                Math.Max(longestStreak, next),
                streakStart ?? activityDate,
                activityDate,
                StreakStatuses.Active,
                freezeCount,
                StreakEventTypes.Continued,
                currentStreak,
                false,
                false);
        }

        if (gap > 1)
        {
            var missedDays = gap - 1;
            if (freezeEnabled && freezeCount > 0 && missedDays == 1)
            {
                // Protect exactly one missed day, then continue.
                var next = currentStreak + 1;
                return new TransitionResult(
                    next,
                    Math.Max(longestStreak, next),
                    streakStart ?? lastActivity.Value,
                    activityDate,
                    StreakStatuses.Frozen,
                    freezeCount - 1,
                    StreakEventTypes.Frozen,
                    currentStreak,
                    true,
                    false);
            }

            // Broken → new streak
            return new TransitionResult(
                1,
                longestStreak,
                activityDate,
                activityDate,
                StreakStatuses.Active,
                freezeCount,
                StreakEventTypes.Broken,
                currentStreak,
                false,
                false);
        }

        // activityDate < lastActivity (clock skew / out of order) — ignore
        return new TransitionResult(
            currentStreak,
            longestStreak,
            streakStart ?? lastActivity.Value,
            lastActivity.Value,
            StreakStatuses.Active,
            freezeCount,
            null,
            null,
            false,
            true);
    }

    public static string ResolveDisplayStatus(
        int currentStreak,
        DateOnly? lastActivity,
        DateOnly today,
        string storedStatus)
    {
        if (currentStreak <= 0)
        {
            return StreakStatuses.Broken;
        }

        if (lastActivity == today)
        {
            return StreakStatuses.Active;
        }

        if (lastActivity == today.AddDays(-1))
        {
            return StreakStatuses.AtRisk;
        }

        if (storedStatus == StreakStatuses.Frozen && lastActivity == today.AddDays(-1))
        {
            return StreakStatuses.Frozen;
        }

        if (lastActivity is not null && lastActivity < today.AddDays(-1))
        {
            return StreakStatuses.Broken;
        }

        return storedStatus;
    }
}

public sealed class StreakService(
    IQualifyingActivityService activityService,
    IStreakStore store,
    IStreakTimeService time,
    IXpEngine xpEngine,
    IAuditLogService audit,
    IEnumerable<IAchievementActivitySink> achievementSinks,
    IOptions<StreakOptions> options,
    ILogger<StreakService> logger) : IStreakService, IMissionActivitySink, IStreakProgressProvider
{
    public async Task<StreakProgressSnapshot> GetSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var status = await GetCurrentAsync(userId, cancellationToken);
        return new StreakProgressSnapshot(
            status.CurrentStreak,
            status.LongestStreak,
            status.Status,
            status.TodayQualified,
            status.FreezeCount,
            status.NextMilestone?.Days);
    }

    public async Task OnValidSessionEndedAsync(
        Guid userId,
        Guid gameId,
        string sessionId,
        int activeSeconds,
        CancellationToken cancellationToken = default)
    {
        await ProcessQualifyingSessionAsync(userId, sessionId, cancellationToken);
    }

    public Task OnFavoriteAddedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask; // Favorites do not qualify for streak days.

    public async Task ProcessQualifyingSessionAsync(
        Guid userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var day = await activityService.RecordValidSessionAsync(userId, sessionId, cancellationToken);
        if (day is null || !day.WasNewDay)
        {
            return;
        }

        var activityDate = DateOnly.FromDateTime(day.ActivityDate);
        await ApplyActivityDayAsync(userId, activityDate, cancellationToken);
    }

    public async Task<StreakStatusDto> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await store.GetStreakAsync(userId, cancellationToken);
        return await ToStatusDtoAsync(row, cancellationToken);
    }

    public async Task<PagedStreakHistoryDto> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? eventType,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await store.GetHistoryAsync(userId, page, pageSize, eventType, from, to, cancellationToken);
        return new PagedStreakHistoryDto(items, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), total);
    }

    public async Task GrantFreezeAsync(
        Guid userId,
        Guid adminId,
        int amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException("Amount must be > 0.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException("Reason is required.");
        }

        var row = await store.GetStreakAsync(userId, cancellationToken);
        var max = options.Value.Freeze.MaxStored;
        var before = row.StreakFreezeCount;
        row.StreakFreezeCount = Math.Min(max, row.StreakFreezeCount + amount);
        await store.SaveStreakAsync(row, cancellationToken);
        await store.InsertHistoryAsync(
            userId, StreakEventTypes.AdminFreezeGranted, row.CurrentStreak, time.TodayLocal(),
            before, row.StreakFreezeCount, reason.Trim(), new { amount, adminId }, cancellationToken);
        await audit.WriteAsync(
            adminId, "ADMIN_FREEZE_GRANTED", "User", userId.ToString("D"),
            new { freezeCount = before }, new { freezeCount = row.StreakFreezeCount }, reason.Trim(),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveFreezeAsync(
        Guid userId,
        Guid adminId,
        int amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException("Amount must be > 0.");
        }

        var row = await store.GetStreakAsync(userId, cancellationToken);
        var before = row.StreakFreezeCount;
        row.StreakFreezeCount = Math.Max(0, row.StreakFreezeCount - amount);
        await store.SaveStreakAsync(row, cancellationToken);
        await store.InsertHistoryAsync(
            userId, StreakEventTypes.AdminFreezeRemoved, row.CurrentStreak, time.TodayLocal(),
            before, row.StreakFreezeCount, reason, new { amount, adminId }, cancellationToken);
        await audit.WriteAsync(
            adminId, "ADMIN_FREEZE_REMOVED", "User", userId.ToString("D"),
            new { freezeCount = before }, new { freezeCount = row.StreakFreezeCount }, reason,
            cancellationToken: cancellationToken);
    }

    public async Task ResetStreakAsync(
        Guid userId,
        Guid adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new GameDiscoveries.BuildingBlocks.Errors.ValidationException("Reason is required.");
        }

        var row = await store.GetStreakAsync(userId, cancellationToken);
        var before = new { row.CurrentStreak, row.LongestStreak, row.StreakStatus, row.StreakFreezeCount };
        var prev = row.CurrentStreak;
        row.CurrentStreak = 0;
        row.StreakStartDate = null;
        row.LastQualifyingActivityDate = null;
        row.StreakStatus = StreakStatuses.Broken;
        await store.SaveStreakAsync(row, cancellationToken);
        await store.InsertHistoryAsync(
            userId, StreakEventTypes.AdminReset, 0, time.TodayLocal(), prev, 0, reason.Trim(),
            new { adminId }, cancellationToken);
        await audit.WriteAsync(
            adminId, "ADMIN_STREAK_RESET", "User", userId.ToString("D"),
            before, new { currentStreak = 0, status = StreakStatuses.Broken }, reason.Trim(),
            cancellationToken: cancellationToken);
    }

    public Task<AdminStreakOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        store.GetOverviewAsync(cancellationToken);

    public async Task<AdminUserStreakDto> GetAdminUserStreakAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var streak = await GetCurrentAsync(userId, cancellationToken);
        var (history, _) = await store.GetHistoryAsync(userId, 1, 20, null, null, null, cancellationToken);
        return new AdminUserStreakDto(userId, streak, history);
    }

    private async Task ApplyActivityDayAsync(Guid userId, DateOnly activityDate, CancellationToken cancellationToken)
    {
        var row = await store.GetStreakAsync(userId, cancellationToken);
        var previous = row.CurrentStreak;
        var last = ToDateOnly(row.LastQualifyingActivityDate);
        var start = ToDateOnly(row.StreakStartDate);

        var result = StreakEngine.ApplyActivity(
            row.CurrentStreak,
            row.LongestStreak,
            start,
            last,
            row.StreakFreezeCount,
            options.Value.Freeze.Enabled,
            activityDate);

        if (result.SameDayNoOp)
        {
            return;
        }

        if (result.EventType == StreakEventTypes.Broken && result.PreviousStreak is > 0)
        {
            await store.InsertHistoryAsync(
                userId, StreakEventTypes.Broken, result.PreviousStreak.Value, activityDate,
                result.PreviousStreak, 0, "Missed day without freeze", null, cancellationToken);

            await store.InsertHistoryAsync(
                userId, StreakEventTypes.Started, 1, activityDate,
                result.PreviousStreak, 1, "New streak after break", null, cancellationToken);
        }
        else if (result.EventType == StreakEventTypes.Frozen)
        {
            await store.InsertHistoryAsync(
                userId, StreakEventTypes.Frozen, result.CurrentStreak, activityDate,
                previous, result.CurrentStreak, "Freeze consumed for missed day",
                new { remainingFreezes = result.FreezeCount }, cancellationToken);

            // After freeze protection, treat as continued streak day
            await store.InsertHistoryAsync(
                userId, StreakEventTypes.Continued, result.CurrentStreak, activityDate,
                previous, result.CurrentStreak, "Continued after freeze", null, cancellationToken);

            result = result with { Status = StreakStatuses.Active };
        }
        else if (result.EventType is not null)
        {
            await store.InsertHistoryAsync(
                userId, result.EventType, result.CurrentStreak, activityDate,
                result.PreviousStreak, result.CurrentStreak, null, null, cancellationToken);
        }

        row.CurrentStreak = result.CurrentStreak;
        row.LongestStreak = result.LongestStreak;
        row.StreakStartDate = result.StreakStartDate.ToDateTime(TimeOnly.MinValue);
        row.LastQualifyingActivityDate = result.LastQualifyingActivityDate.ToDateTime(TimeOnly.MinValue);
        row.StreakStatus = result.Status;
        row.StreakFreezeCount = result.FreezeCount;
        await store.SaveStreakAsync(row, cancellationToken);

        foreach (var sink in achievementSinks)
        {
            await sink.OnStreakProgressAsync(userId, result.CurrentStreak, result.LongestStreak, cancellationToken);
        }

        await ProcessMilestonesAsync(userId, previous, result.CurrentStreak, cancellationToken);

        logger.LogInformation(
            "streak_updated userId={UserId} previous={Previous} current={Current} event={Event}",
            userId, previous, result.CurrentStreak, result.EventType);
    }

    private async Task ProcessMilestonesAsync(
        Guid userId,
        int previousStreak,
        int currentStreak,
        CancellationToken cancellationToken)
    {
        if (currentStreak <= previousStreak)
        {
            return;
        }

        var milestones = await store.GetActiveMilestonesAsync(cancellationToken);
        foreach (var milestone in milestones.Where(m => m.Days > previousStreak && m.Days <= currentStreak))
        {
            if (await store.HasAchievedMilestoneAsync(userId, milestone.Days, cancellationToken))
            {
                continue;
            }

            var rewardXp = milestone.RewardXp ?? 0;
            await store.RecordMilestoneAsync(userId, milestone.Days, rewardXp, cancellationToken);
            await store.InsertHistoryAsync(
                userId, StreakEventTypes.Milestone, currentStreak, time.TodayLocal(),
                previousStreak, currentStreak, milestone.Title,
                new { days = milestone.Days, rewardXp }, cancellationToken);

            if (rewardXp > 0)
            {
                try
                {
                    await xpEngine.AwardAsync(
                        new XpAwardRequest(
                            userId,
                    XpRuleCodes.StreakMilestone,
                    "STREAK_MILESTONE",
                    "STREAK_MILESTONE",
                    $"{userId:D}:{milestone.Days}",
                    rewardXp,
                            $"{milestone.Title} ({milestone.Days}-day streak)",
                            new Dictionary<string, object?> { ["days"] = milestone.Days }),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to award streak milestone XP for user {UserId} days={Days}", userId, milestone.Days);
                }
            }
        }
    }

    private async Task<StreakStatusDto> ToStatusDtoAsync(UserStreakRow row, CancellationToken cancellationToken)
    {
        var today = time.TodayLocal();
        var last = ToDateOnly(row.LastQualifyingActivityDate);
        var start = ToDateOnly(row.StreakStartDate);
        var todayQualified = last == today;
        var status = StreakEngine.ResolveDisplayStatus(row.CurrentStreak, last, today, row.StreakStatus);

        // Lazy: if last activity was yesterday and status stored as ACTIVE, show AT_RISK without write
        var milestones = await store.GetActiveMilestonesAsync(cancellationToken);
        var next = milestones.FirstOrDefault(m => m.Days > row.CurrentStreak);
        StreakMilestoneInfo? nextInfo = next is null
            ? null
            : new StreakMilestoneInfo(next.Days, next.Title, next.Days - row.CurrentStreak, next.RewardXp);

        StreakMilestoneInfo? lastAchieved = null;
        var achieved = milestones.Where(m => m.Days <= row.CurrentStreak).OrderByDescending(m => m.Days).FirstOrDefault();
        if (achieved is not null)
        {
            lastAchieved = new StreakMilestoneInfo(achieved.Days, achieved.Title, 0, achieved.RewardXp);
        }

        return new StreakStatusDto(
            row.CurrentStreak,
            row.LongestStreak,
            status,
            start,
            last,
            todayQualified,
            row.StreakFreezeCount,
            options.Value.Freeze.MaxStored,
            nextInfo,
            lastAchieved);
    }

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);
}

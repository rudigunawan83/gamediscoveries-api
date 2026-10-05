using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Missions.Data;
using GameDiscoveries.Modules.Missions.Domain;
using GameDiscoveries.Modules.Missions.Models;
using GameDiscoveries.Modules.Missions.Options;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Missions.Services;

public interface IMissionService
{
    Task<MyMissionsResponse> GetMyMissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedMissionsResponse> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? type,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<MissionDto?> GetMissionAsync(Guid userId, Guid missionId, CancellationToken cancellationToken = default);

    Task RefreshProgressAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class MissionService(
    IMissionStore store,
    IMissionAssignmentService assignment,
    IMissionPeriodService periods,
    IXpEngine xpEngine,
    IEnumerable<IAchievementActivitySink> achievementSinks,
    IOptions<MissionsOptions> options,
    ILogger<MissionService> logger) : IMissionService, IMissionActivitySink
{
    public async Task OnValidSessionEndedAsync(
        Guid userId,
        Guid gameId,
        string sessionId,
        int activeSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await assignment.EnsureAssignedAsync(userId, cancellationToken);
        await RefreshProgressAsync(userId, cancellationToken);
    }

    public async Task OnFavoriteAddedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await assignment.EnsureAssignedAsync(userId, cancellationToken);
        await RefreshProgressAsync(userId, cancellationToken);
    }

    public async Task<MyMissionsResponse> GetMyMissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            var emptyDaily = periods.GetDailyPeriod(DateTimeOffset.UtcNow);
            var emptyWeekly = periods.GetWeeklyPeriod(DateTimeOffset.UtcNow);
            return new MyMissionsResponse([], [], emptyDaily.End, emptyWeekly.End, options.Value.TimeZone);
        }

        await assignment.EnsureAssignedAsync(userId, cancellationToken);
        await RefreshProgressAsync(userId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var dailyPeriod = periods.GetDailyPeriod(now);
        var weeklyPeriod = periods.GetWeeklyPeriod(now);

        var daily = await store.GetUserMissionsForPeriodAsync(userId, MissionTypes.Daily, dailyPeriod.Start, cancellationToken);
        var weekly = await store.GetUserMissionsForPeriodAsync(userId, MissionTypes.Weekly, weeklyPeriod.Start, cancellationToken);

        // Include icon from templates when available
        var templates = await store.GetAllTemplatesAsync(cancellationToken);
        var iconByCode = templates.ToDictionary(t => t.Code, t => t.Icon, StringComparer.OrdinalIgnoreCase);
        var difficultyByCode = templates.ToDictionary(t => t.Code, t => t.Difficulty, StringComparer.OrdinalIgnoreCase);

        return new MyMissionsResponse(
            daily.Select(m => ToDto(m, iconByCode, difficultyByCode)).ToList(),
            weekly.Select(m => ToDto(m, iconByCode, difficultyByCode)).ToList(),
            dailyPeriod.End,
            weeklyPeriod.End,
            options.Value.TimeZone);
    }

    public async Task<PagedMissionsResponse> GetHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? type,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await store.GetHistoryAsync(userId, page, pageSize, type, status, from, to, cancellationToken);
        var templates = await store.GetAllTemplatesAsync(cancellationToken);
        var iconByCode = templates.ToDictionary(t => t.Code, t => t.Icon, StringComparer.OrdinalIgnoreCase);
        var difficultyByCode = templates.ToDictionary(t => t.Code, t => t.Difficulty, StringComparer.OrdinalIgnoreCase);
        return new PagedMissionsResponse(
            items.Select(m => ToDto(m, iconByCode, difficultyByCode)).ToList(),
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100),
            total);
    }

    public async Task<MissionDto?> GetMissionAsync(
        Guid userId,
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        var mission = await store.GetUserMissionAsync(userId, missionId, cancellationToken);
        if (mission is null)
        {
            return null;
        }

        var template = await store.GetTemplateByCodeAsync(mission.Code, cancellationToken);
        var icons = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [mission.Code] = template?.Icon
        };
        var diffs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [mission.Code] = template?.Difficulty ?? MissionDifficulties.Medium
        };
        return ToDto(mission, icons, diffs);
    }

    public async Task RefreshProgressAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await store.ExpirePastMissionsAsync(userId, now, cancellationToken);
        var active = await store.GetActiveUserMissionsAsync(userId, cancellationToken);
        var tzId = options.Value.TimeZone;

        foreach (var mission in active)
        {
            if (mission.PeriodEnd < now)
            {
                continue;
            }

            var progress = await store.ComputeProgressAsync(
                userId,
                mission.RequirementType,
                mission.PeriodStart,
                mission.PeriodEnd,
                tzId,
                cancellationToken);

            if (progress == mission.ProgressValue && progress < mission.TargetValue)
            {
                continue;
            }

            var (updated, justCompleted) = await store.UpdateProgressAndCompleteAsync(
                mission.Id,
                userId,
                progress,
                cancellationToken);

            if (justCompleted)
            {
                await AwardMissionXpAsync(updated, cancellationToken);
            }
        }
    }

    private async Task AwardMissionXpAsync(UserMissionEntity mission, CancellationToken cancellationToken)
    {
        if (mission.RewardTransactionId is not null || mission.RewardXp <= 0)
        {
            return;
        }

        var isWeekly = string.Equals(mission.Type, MissionTypes.Weekly, StringComparison.OrdinalIgnoreCase);
        var ruleCode = isWeekly ? XpRuleCodes.WeeklyChallengeCompleted : XpRuleCodes.DailyMissionCompleted;
        var eventType = isWeekly ? "WEEKLY_CHALLENGE_COMPLETED" : "DAILY_MISSION_COMPLETED";

        try
        {
            var result = await xpEngine.AwardAsync(
                new XpAwardRequest(
                    mission.UserId,
                    ruleCode,
                    eventType,
                    XpReferenceTypes.UserMission,
                    mission.Id.ToString("D"),
                    mission.RewardXp,
                    $"{mission.Title} completed",
                    new Dictionary<string, object?>
                    {
                        ["missionCode"] = mission.Code,
                        ["missionType"] = mission.Type
                    }),
                cancellationToken);

            if (result.XpAwarded > 0 || result.Reason is "ALREADY_REWARDED" or null)
            {
                // Look up transaction id is optional; store a synthetic marker via mission id when awarded
                await store.MarkRewardTransactionAsync(mission.Id, mission.Id, cancellationToken);

                foreach (var sink in achievementSinks)
                {
                    await sink.OnMissionCompletedAsync(mission.UserId, mission.Type, cancellationToken);
                }
            }

            logger.LogInformation(
                "mission_rewarded userId={UserId} missionId={MissionId} code={Code} xp={Xp} reason={Reason}",
                mission.UserId,
                mission.Id,
                mission.Code,
                result.XpAwarded,
                result.Reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to award mission XP for {MissionId}", mission.Id);
        }
    }

    private static MissionDto ToDto(
        UserMissionEntity m,
        IReadOnlyDictionary<string, string?> icons,
        IReadOnlyDictionary<string, string> difficulties)
    {
        var pct = m.TargetValue <= 0
            ? 0
            : Math.Round(100d * Math.Min(m.ProgressValue, m.TargetValue) / m.TargetValue, 2);

        icons.TryGetValue(m.Code, out var icon);
        difficulties.TryGetValue(m.Code, out var difficulty);

        return new MissionDto(
            m.Id,
            m.Code,
            m.Type,
            m.Title,
            m.Description,
            icon,
            m.RequirementType,
            m.ProgressValue,
            m.TargetValue,
            pct,
            m.RewardXp,
            m.Status,
            difficulty ?? MissionDifficulties.Medium,
            m.PeriodStart,
            m.PeriodEnd,
            m.CompletedAt);
    }
}

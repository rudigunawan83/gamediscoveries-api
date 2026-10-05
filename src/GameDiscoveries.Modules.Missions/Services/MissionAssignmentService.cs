using GameDiscoveries.Modules.Missions.Data;
using GameDiscoveries.Modules.Missions.Domain;
using GameDiscoveries.Modules.Missions.Models;
using GameDiscoveries.Modules.Missions.Options;

namespace GameDiscoveries.Modules.Missions.Services;

public interface IMissionSelectionStrategy
{
    string Name { get; }

    IReadOnlyList<MissionTemplateEntity> SelectDaily(
        IReadOnlyList<MissionTemplateEntity> activeDaily,
        bool isNewUser,
        int count);

    IReadOnlyList<MissionTemplateEntity> SelectWeekly(
        IReadOnlyList<MissionTemplateEntity> activeWeekly,
        int count);
}

public sealed class DefaultMissionSelectionStrategy : IMissionSelectionStrategy
{
    public string Name => "Default";

    public IReadOnlyList<MissionTemplateEntity> SelectDaily(
        IReadOnlyList<MissionTemplateEntity> activeDaily,
        bool isNewUser,
        int count)
    {
        if (activeDaily.Count == 0 || count <= 0)
        {
            return [];
        }

        var pool = activeDaily.ToList();
        var selected = new List<MissionTemplateEntity>();

        if (isNewUser)
        {
            // Prefer easy/medium for new users
            TryPick(pool, selected, t => t.Difficulty == MissionDifficulties.Easy);
            TryPick(pool, selected, t =>
                t.Difficulty != MissionDifficulties.Hard &&
                (IsExploration(t) || t.Code is MissionCodes.DiscoverNewGame or MissionCodes.FavoriteAGame));
            TryPick(pool, selected, t => t.Difficulty == MissionDifficulties.Medium);
            // Avoid hard missions for new users unless nothing else remains
            TryPick(pool, selected, t => t.Difficulty != MissionDifficulties.Hard);
        }
        else
        {
            TryPick(pool, selected, t => t.Difficulty == MissionDifficulties.Easy);
            TryPick(pool, selected, t => t.Difficulty == MissionDifficulties.Medium);
            TryPick(pool, selected, IsExploration);
        }

        // Fallback fill — for new users, soft-prefer non-hard first
        var fallback = isNewUser
            ? pool.OrderBy(x => x.Difficulty == MissionDifficulties.Hard ? 1 : 0)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Code)
            : pool.OrderBy(x => x.SortOrder).ThenBy(x => x.Code);

        foreach (var t in fallback)
        {
            if (selected.Count >= count)
            {
                break;
            }

            if (selected.All(s => s.Id != t.Id))
            {
                selected.Add(t);
            }
        }

        return selected.Take(count).ToList();
    }

    public IReadOnlyList<MissionTemplateEntity> SelectWeekly(
        IReadOnlyList<MissionTemplateEntity> activeWeekly,
        int count)
    {
        if (activeWeekly.Count == 0 || count <= 0)
        {
            return [];
        }

        var pool = activeWeekly.ToList();
        var selected = new List<MissionTemplateEntity>();
        TryPick(pool, selected, t => t.Difficulty == MissionDifficulties.Medium);
        TryPick(pool, selected, t => t.Difficulty == MissionDifficulties.Hard);

        foreach (var t in pool.OrderBy(x => x.SortOrder).ThenBy(x => x.Code))
        {
            if (selected.Count >= count)
            {
                break;
            }

            if (selected.All(s => s.Id != t.Id))
            {
                selected.Add(t);
            }
        }

        return selected.Take(count).ToList();
    }

    private static bool IsExploration(MissionTemplateEntity t) =>
        t.Code.Contains("DISCOVER", StringComparison.OrdinalIgnoreCase) ||
        t.Code.Contains("EXPLORE", StringComparison.OrdinalIgnoreCase) ||
        t.Code.Contains("GENRE", StringComparison.OrdinalIgnoreCase);

    private static void TryPick(
        List<MissionTemplateEntity> pool,
        List<MissionTemplateEntity> selected,
        Func<MissionTemplateEntity, bool> predicate)
    {
        var candidate = pool
            .Where(predicate)
            .Where(t => selected.All(s => s.Id != t.Id))
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Code)
            .FirstOrDefault();

        if (candidate is not null)
        {
            selected.Add(candidate);
        }
    }
}

public interface IMissionAssignmentService
{
    Task EnsureAssignedAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class MissionAssignmentService(
    IMissionStore store,
    IMissionPeriodService periods,
    IMissionSelectionStrategy selection,
    Microsoft.Extensions.Options.IOptions<MissionsOptions> options) : IMissionAssignmentService
{
    public async Task EnsureAssignedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await store.ExpirePastMissionsAsync(userId, now, cancellationToken);

        var totalXp = await store.GetUserTotalXpAsync(userId, cancellationToken);
        var isNewUser = totalXp < options.Value.NewUserXpThreshold;

        await AssignTypeAsync(userId, MissionTypes.Daily, periods.GetDailyPeriod(now), isNewUser, cancellationToken);
        await AssignTypeAsync(userId, MissionTypes.Weekly, periods.GetWeeklyPeriod(now), isNewUser, cancellationToken);
    }

    private async Task AssignTypeAsync(
        Guid userId,
        string type,
        MissionPeriod period,
        bool isNewUser,
        CancellationToken cancellationToken)
    {
        var existing = await store.GetUserMissionsForPeriodAsync(userId, type, period.Start, cancellationToken);
        var targetCount = type == MissionTypes.Daily
            ? options.Value.DailyMissionCount
            : options.Value.WeeklyChallengeCount;

        if (existing.Count >= targetCount)
        {
            return;
        }

        var templates = await store.GetActiveTemplatesAsync(type, cancellationToken);
        var already = existing.Select(e => e.MissionTemplateId).ToHashSet();
        var available = templates.Where(t => !already.Contains(t.Id)).ToList();

        var picked = type == MissionTypes.Daily
            ? selection.SelectDaily(available, isNewUser, targetCount - existing.Count)
            : selection.SelectWeekly(available, targetCount - existing.Count);

        foreach (var template in picked)
        {
            await store.InsertUserMissionAsync(new UserMissionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MissionTemplateId = template.Id,
                Code = template.Code,
                Type = template.Type,
                Title = template.Title,
                Description = template.Description,
                RequirementType = template.RequirementType,
                PeriodStart = period.Start,
                PeriodEnd = period.End,
                ProgressValue = 0,
                TargetValue = template.TargetValue,
                RewardXp = template.RewardXp,
                Status = MissionStatuses.Active
            }, cancellationToken);
        }
    }
}

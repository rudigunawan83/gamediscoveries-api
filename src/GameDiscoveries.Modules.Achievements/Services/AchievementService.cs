using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Achievements.Data;
using GameDiscoveries.Modules.Achievements.Domain;
using GameDiscoveries.Modules.Achievements.Evaluation;
using GameDiscoveries.Modules.Achievements.Models;
using GameDiscoveries.Modules.Achievements.Options;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Services;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Achievements.Services;

public interface IAchievementService
{
    Task EvaluateForUserAsync(Guid userId, string trigger, CancellationToken cancellationToken = default);

    Task<AchievementListResponse> GetUserAchievementsAsync(
        Guid userId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementHistoryDto>> GetRecentAsync(Guid userId, int limit, CancellationToken cancellationToken = default);

    Task<AchievementDto> GetByCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AdminAchievementDefinitionDto> Items, AdminAchievementOverviewStats Overview)> GetAdminDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<AdminAchievementDefinitionDto> GetAdminDefinitionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdminAchievementDefinitionDto> CreateAsync(
        Guid adminId,
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminAchievementDefinitionDto> UpdateAsync(
        Guid adminId,
        Guid id,
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid adminId, Guid id, bool isActive, string? reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminAchievementUserDto>> GetUsersAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementDto>> GetAdminUserAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task GrantAsync(Guid adminId, Guid userId, Guid definitionId, string? reason, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid adminId, Guid userId, Guid definitionId, string? reason, CancellationToken cancellationToken = default);
}

public sealed class AchievementService(
    IAchievementStore store,
    IRequirementEvaluator evaluator,
    IServiceProvider services,
    IAnalyticsEventService analytics,
    IAuditLogService audit,
    IOptions<AchievementOptions> options,
    ILogger<AchievementService> logger) : IAchievementService, IAchievementActivitySink
{
    public Task OnValidSessionEndedAsync(
        Guid userId,
        Guid gameId,
        string sessionId,
        int activeSeconds,
        CancellationToken cancellationToken = default) =>
        EvaluateForUserAsync(userId, AchievementTriggers.ValidSessionEnded, cancellationToken);

    public Task OnFavoriteAddedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        EvaluateForUserAsync(userId, AchievementTriggers.FavoriteAdded, cancellationToken);

    public Task OnRatingCreatedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        EvaluateForUserAsync(userId, AchievementTriggers.RatingCreated, cancellationToken);

    public Task OnReviewCreatedAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default) =>
        EvaluateForUserAsync(userId, AchievementTriggers.ReviewCreated, cancellationToken);

    public Task OnLevelUpAsync(Guid userId, int newLevel, CancellationToken cancellationToken = default) =>
        EvaluateForUserAsync(userId, AchievementTriggers.LevelUp, cancellationToken);

    public Task OnMissionCompletedAsync(Guid userId, string missionType, CancellationToken cancellationToken = default) =>
        EvaluateForUserAsync(userId, AchievementTriggers.MissionCompleted, cancellationToken);

    public Task OnStreakProgressAsync(
        Guid userId,
        int currentStreak,
        int longestStreak,
        CancellationToken cancellationToken = default) =>
        EvaluateForUserAsync(userId, AchievementTriggers.StreakProgress, cancellationToken);

    public async Task EvaluateForUserAsync(Guid userId, string trigger, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var requirements = AchievementTriggerRequirements.ForTrigger(trigger);
        var definitions = await store.GetDefinitionsAsync(true, requirements, cancellationToken);
        if (definitions.Count == 0)
        {
            return;
        }

        var context = await evaluator.CreateContextAsync(userId, cancellationToken);
        foreach (var definition in definitions)
        {
            await TryUnlockAsync(userId, definition, context, null, null, cancellationToken);
        }
    }

    public async Task<AchievementListResponse> GetUserAchievementsAsync(
        Guid userId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        await EvaluateForUserAsync(userId, AchievementTriggers.Any, cancellationToken);
        var definitions = await store.GetDefinitionsAsync(true, null, cancellationToken);
        var unlocks = await store.GetUnlocksAsync(userId, false, cancellationToken);
        var context = await evaluator.CreateContextAsync(userId, cancellationToken);
        var items = definitions
            .Select(d => ToUserDto(d, unlocks.GetValueOrDefault(d.Id), evaluator.Evaluate(d, context)))
            .Where(x => status?.ToLowerInvariant() switch
            {
                "unlocked" => x.IsUnlocked,
                "in-progress" or "in_progress" => !x.IsUnlocked && x.ProgressValue > 0,
                _ => true
            })
            .ToList();

        var total = definitions.Count;
        var unlocked = unlocks.Count;
        var inProgress = items.Count(x => !x.IsUnlocked && x.ProgressValue > 0);
        var overview = new AchievementOverviewStats(
            total,
            definitions.Count(d => d.IsActive),
            unlocked,
            inProgress,
            total == 0 ? 0 : Math.Round(100d * unlocked / total, 2));

        return new AchievementListResponse(items, overview);
    }

    public Task<IReadOnlyList<AchievementHistoryDto>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        store.GetRecentHistoryAsync(userId, limit, cancellationToken);

    public async Task<AchievementDto> GetByCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        await EvaluateForUserAsync(userId, AchievementTriggers.Any, cancellationToken);
        var definition = await store.GetDefinitionByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("Achievement", code);
        var unlock = await store.GetUnlockAsync(userId, definition.Id, false, cancellationToken);
        var context = await evaluator.CreateContextAsync(userId, cancellationToken);
        return ToUserDto(definition, unlock, evaluator.Evaluate(definition, context));
    }

    public async Task<(IReadOnlyList<AdminAchievementDefinitionDto> Items, AdminAchievementOverviewStats Overview)> GetAdminDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = await store.GetDefinitionsAsync(false, null, cancellationToken);
        var overview = await store.GetOverviewAsync(cancellationToken);
        return (definitions.Select(ToAdminDto).ToList(), overview);
    }

    public async Task<AdminAchievementDefinitionDto> GetAdminDefinitionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var definition = await store.GetDefinitionByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Achievement", id.ToString("D"));
        return ToAdminDto(definition);
    }

    public async Task<AdminAchievementDefinitionDto> CreateAsync(
        Guid adminId,
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var created = await store.CreateDefinitionAsync(request, cancellationToken);
        await store.InsertHistoryAsync(adminId, created.Id, AchievementHistoryEventTypes.Created, adminId, null, null, cancellationToken);
        await audit.WriteAsync(adminId, AuditActions.AdminAchievementCreated, "Achievement", created.Id.ToString("D"),
            null, created, null, cancellationToken: cancellationToken);
        return ToAdminDto(created);
    }

    public async Task<AdminAchievementDefinitionDto> UpdateAsync(
        Guid adminId,
        Guid id,
        UpsertAchievementRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var before = await store.GetDefinitionByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Achievement", id.ToString("D"));
        var updated = await store.UpdateDefinitionAsync(id, request, cancellationToken);
        await store.InsertHistoryAsync(adminId, id, AchievementHistoryEventTypes.Updated, adminId, null, null, cancellationToken);
        await audit.WriteAsync(adminId, AuditActions.AdminAchievementUpdated, "Achievement", id.ToString("D"),
            before, updated, null, cancellationToken: cancellationToken);
        return ToAdminDto(updated);
    }

    public async Task SetActiveAsync(
        Guid adminId,
        Guid id,
        bool isActive,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var before = await store.GetDefinitionByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Achievement", id.ToString("D"));
        await store.SetActiveAsync(id, isActive, cancellationToken);
        var after = await store.GetDefinitionByIdAsync(id, cancellationToken);
        var eventType = isActive ? AchievementHistoryEventTypes.Activated : AchievementHistoryEventTypes.Deactivated;
        await store.InsertHistoryAsync(adminId, id, eventType, adminId, reason, null, cancellationToken);
        await audit.WriteAsync(
            adminId,
            isActive ? AuditActions.AdminAchievementActivated : AuditActions.AdminAchievementDeactivated,
            "Achievement",
            id.ToString("D"),
            before,
            after,
            reason,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<AdminAchievementUserDto>> GetUsersAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        store.GetUsersForDefinitionAsync(id, cancellationToken);

    public async Task<IReadOnlyList<AchievementDto>> GetAdminUserAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetUserAchievementsAsync(userId, null, cancellationToken);
        return response.Items;
    }

    public async Task GrantAsync(
        Guid adminId,
        Guid userId,
        Guid definitionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var definition = await store.GetDefinitionByIdAsync(definitionId, cancellationToken)
            ?? throw new NotFoundException("Achievement", definitionId.ToString("D"));
        var unlock = await store.TryInsertUnlockAsync(userId, definition, definition.TargetValue, adminId, cancellationToken);
        if (unlock is null)
        {
            return;
        }

        await AwardXpAsync(userId, definition, unlock.Id, cancellationToken);
        await store.InsertHistoryAsync(userId, definition.Id, AchievementHistoryEventTypes.Granted, adminId, reason, null, cancellationToken);
        await audit.WriteAsync(adminId, AuditActions.AdminAchievementGranted, "AchievementUnlock", unlock.Id.ToString("D"),
            null, new { userId, definitionId }, reason, cancellationToken: cancellationToken);
    }

    public async Task RevokeAsync(
        Guid adminId,
        Guid userId,
        Guid definitionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var revoked = await store.RevokeUnlockAsync(userId, definitionId, adminId, reason, cancellationToken);
        if (!revoked)
        {
            return;
        }

        await store.InsertHistoryAsync(userId, definitionId, AchievementHistoryEventTypes.Revoked, adminId, reason, null, cancellationToken);
        await audit.WriteAsync(adminId, AuditActions.AdminAchievementRevoked, "AchievementUnlock", $"{userId:D}:{definitionId:D}",
            null, new { userId, definitionId, revoked = true }, reason, cancellationToken: cancellationToken);
    }

    private async Task TryUnlockAsync(
        Guid userId,
        AchievementDefinitionEntity definition,
        RequirementProgressContext context,
        Guid? adminId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!definition.IsActive)
        {
            return;
        }

        var progress = evaluator.Evaluate(definition, context);
        if (!progress.IsComplete)
        {
            return;
        }

        var unlock = await store.TryInsertUnlockAsync(userId, definition, progress.CurrentValue, adminId, cancellationToken);
        if (unlock is null)
        {
            return;
        }

        await AwardXpAsync(userId, definition, unlock.Id, cancellationToken);
        await store.InsertHistoryAsync(
            userId,
            definition.Id,
            adminId is null ? AchievementHistoryEventTypes.Unlocked : AchievementHistoryEventTypes.Granted,
            adminId,
            reason,
            new { progress = progress.CurrentValue, target = progress.TargetValue },
            cancellationToken);
        await EmitAnalyticsAsync(userId, definition, progress, cancellationToken);

        logger.LogInformation(
            "achievement_unlocked userId={UserId} code={Code} progress={Progress} target={Target}",
            userId,
            definition.Code,
            progress.CurrentValue,
            progress.TargetValue);
    }

    private async Task AwardXpAsync(
        Guid userId,
        AchievementDefinitionEntity definition,
        Guid unlockId,
        CancellationToken cancellationToken)
    {
        if (definition.RewardXp <= 0)
        {
            return;
        }

        // Resolved lazily: XpEngine notifies achievement sinks, so injecting it would form a cycle.
        var xpEngine = services.GetRequiredService<IXpEngine>();
        var result = await xpEngine.AwardAsync(
            new XpAwardRequest(
                userId,
                XpRuleCodes.AchievementUnlock,
                AnalyticsEventTypes.AchievementUnlocked,
                XpReferenceTypes.Achievement,
                $"{userId:D}:{definition.Id:D}",
                definition.RewardXp,
                $"Achievement unlocked: {definition.Title}",
                new Dictionary<string, object?>
                {
                    ["achievementCode"] = definition.Code,
                    ["achievementId"] = definition.Id
                }),
            cancellationToken);

        if (result.XpAwarded > 0 || result.Reason is "ALREADY_REWARDED" or null)
        {
            await store.SetRewardTransactionAsync(unlockId, unlockId, cancellationToken);
        }
    }

    private async Task EmitAnalyticsAsync(
        Guid userId,
        AchievementDefinitionEntity definition,
        RequirementProgressResult progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await analytics.TrackAsync(
                new AnalyticsEventIngestRequest(
                    Guid.NewGuid(),
                    AnalyticsEventTypes.AchievementUnlocked,
                    null,
                    null,
                    null,
                    AnalyticsSources.System,
                    AnalyticsPlatforms.Unknown,
                    null,
                    null,
                    null,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["achievementCode"] = definition.Code,
                        ["achievementId"] = definition.Id,
                        ["category"] = definition.Category,
                        ["difficulty"] = definition.Difficulty,
                        ["progress"] = progress.CurrentValue,
                        ["target"] = progress.TargetValue
                    },
                    DateTimeOffset.UtcNow,
                    userId),
                userId,
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to emit achievement analytics for {Code}", definition.Code);
        }
    }

    public static AchievementDto ToUserDto(
        AchievementDefinitionEntity definition,
        AchievementUnlockEntity? unlock,
        RequirementProgressResult progress)
    {
        var isUnlocked = unlock is not null;
        if (definition.IsSecret && !isUnlocked)
        {
            return new AchievementDto(
                definition.Id,
                definition.Code,
                "???",
                "Keep exploring to discover this achievement.",
                definition.Category,
                definition.Difficulty,
                definition.Icon,
                true,
                false,
                null,
                0,
                0,
                0,
                0);
        }

        return new AchievementDto(
            definition.Id,
            definition.Code,
            definition.Title,
            definition.Description,
            definition.Category,
            definition.Difficulty,
            definition.Icon,
            definition.IsSecret,
            isUnlocked,
            unlock?.UnlockedAt,
            isUnlocked ? unlock!.ProgressValue : progress.CurrentValue,
            isUnlocked ? unlock!.TargetValue : progress.TargetValue,
            isUnlocked ? 100 : progress.Percentage,
            definition.RewardXp);
    }

    private static AdminAchievementDefinitionDto ToAdminDto(AchievementDefinitionEntity definition) =>
        new(
            definition.Id,
            definition.Code,
            definition.Title,
            definition.Description,
            definition.Category,
            definition.Difficulty,
            definition.RequirementType,
            definition.TargetValue,
            definition.RewardXp,
            definition.Icon,
            definition.IsSecret,
            definition.IsActive,
            definition.SeasonId,
            definition.CreatedAt,
            definition.UpdatedAt,
            definition.UnlockedCount);

    private static void ValidateRequest(UpsertAchievementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ValidationException("Code, title, and description are required.");
        }

        if (!AchievementCategories.All.Contains(request.Category))
        {
            throw new ValidationException("Invalid achievement category.");
        }

        if (!AchievementDifficulties.All.Contains(request.Difficulty))
        {
            throw new ValidationException("Invalid achievement difficulty.");
        }

        if (!AchievementRequirementTypes.All.Contains(request.RequirementType))
        {
            throw new ValidationException("Invalid achievement requirement type.");
        }

        if (request.TargetValue <= 0 || request.RewardXp < 0)
        {
            throw new ValidationException("Target value must be positive and reward XP cannot be negative.");
        }
    }
}

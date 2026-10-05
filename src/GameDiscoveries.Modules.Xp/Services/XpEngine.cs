using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Services;
using GameDiscoveries.Modules.Xp.Data;
using GameDiscoveries.Modules.Xp.Domain;
using GameDiscoveries.Modules.Xp.Models;
using GameDiscoveries.Modules.Xp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Xp.Services;

public interface IXpEngine
{
    Task<XpAwardResult> AwardAsync(XpAwardRequest request, CancellationToken cancellationToken = default);

    Task<XpAwardResult> ProcessGameSessionEndAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> ProcessFavoriteAddedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> ProcessRatingCreatedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> ProcessReviewCreatedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> AdminAdjustAsync(
        Guid userId,
        Guid adminId,
        int xpAmount,
        string description,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> ReverseAsync(
        Guid transactionId,
        Guid adminId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<UserXpSummary> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<XpTransactionDto>> GetHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        string? ruleCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}

public sealed class XpEngine(
    IXpStore store,
    IXpRuleCatalog catalog,
    IOptions<XpOptions> options,
    IAnalyticsEventService analytics,
    IEnumerable<IAchievementActivitySink> achievementSinks,
    IEnumerable<ILeaderboardXpSink> leaderboardSinks,
    ILogger<XpEngine> logger) : IXpEngine
{
    public async Task<XpAwardResult> AwardAsync(XpAwardRequest request, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            return new XpAwardResult(true, 0, 0, "XP_DISABLED", []);
        }

        if (request.XpAmount == 0)
        {
            return new XpAwardResult(true, 0, 0, "ZERO_AMOUNT", []);
        }

        var result = await store.TryAwardAsync(request, opts.DailyXpCap, cancellationToken);

        if (result.XpAwarded != 0)
        {
            logger.LogInformation(
                "xp_awarded userId={UserId} ruleCode={RuleCode} xp={Xp} referenceType={ReferenceType} referenceId={ReferenceId} leveledUp={LeveledUp}",
                request.UserId,
                request.RuleCode,
                result.XpAwarded,
                request.ReferenceType,
                request.ReferenceId,
                result.LeveledUp);

            await NotifyLeaderboardsAsync(request, result.XpAwarded, cancellationToken);

            if (result.LeveledUp && result.PreviousLevel is not null && result.CurrentLevel is not null)
            {
                await EmitLevelUpAsync(
                    request.UserId,
                    result.PreviousLevel.Value,
                    result.CurrentLevel.Value,
                    result.TotalXp,
                    cancellationToken);
            }
        }
        else if (result.Reason == "DAILY_XP_CAP_REACHED")
        {
            logger.LogInformation("xp_daily_cap_reached userId={UserId} ruleCode={RuleCode}", request.UserId, request.RuleCode);
        }
        else if (result.Reason == "ALREADY_REWARDED")
        {
            logger.LogInformation(
                "xp_duplicate userId={UserId} ruleCode={RuleCode} referenceId={ReferenceId}",
                request.UserId,
                request.RuleCode,
                request.ReferenceId);
        }
        else
        {
            logger.LogInformation(
                "xp_rejected userId={UserId} ruleCode={RuleCode} reason={Reason}",
                request.UserId,
                request.RuleCode,
                result.Reason);
        }

        return result;
    }

    public async Task<XpAwardResult> ProcessGameSessionEndAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return new XpAwardResult(true, 0, 0, "XP_DISABLED", []);
        }

        var session = await store.GetSessionSnapshotAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return new XpAwardResult(false, 0, 0, "SESSION_NOT_FOUND", []);
        }

        if (session.UserId is null)
        {
            return new XpAwardResult(true, 0, 0, "ANONYMOUS_NO_XP", []);
        }

        if (!session.IsValid)
        {
            logger.LogInformation(
                "xp_rejected userId={UserId} reason=SESSION_NOT_VALID sessionId={SessionId}",
                session.UserId,
                session.SessionId);
            return new XpAwardResult(true, 0, 0, "SESSION_NOT_VALID", []);
        }

        var userId = session.UserId.Value;
        var awarded = new List<XpAwardedItem>();
        long totalXp = 0;
        string? lastReason = null;
        int? previousLevel = null;
        int? currentLevel = null;
        var leveledUp = false;
        LevelInfo? level = null;

        async Task Apply(string ruleCode, string referenceType, string referenceId, string description)
        {
            var rule = catalog.Get(ruleCode);
            if (rule is null || !rule.IsActive)
            {
                return;
            }

            var result = await AwardAsync(
                new XpAwardRequest(
                    userId,
                    ruleCode,
                    "GAME_SESSION_END",
                    referenceType,
                    referenceId,
                    rule.XpAmount,
                    description,
                    new Dictionary<string, object?>
                    {
                        ["sessionId"] = session.SessionId,
                        ["gameId"] = session.GameId,
                        ["activeSeconds"] = session.ActiveSeconds
                    }),
                cancellationToken);

            totalXp = result.TotalXp;
            lastReason = result.Reason;
            awarded.AddRange(result.Transactions);
            previousLevel ??= result.PreviousLevel;
            if (result.CurrentLevel is not null)
            {
                currentLevel = result.CurrentLevel;
            }

            if (result.LeveledUp)
            {
                leveledUp = true;
            }

            if (result.Level is not null)
            {
                level = result.Level;
            }
        }

        var hadPriorValid = await store.HasValidSessionBeforeAsync(userId, session.SessionId, cancellationToken);
        if (!hadPriorValid)
        {
            await Apply(
                XpRuleCodes.FirstGameDiscovery,
                XpReferenceTypes.User,
                userId.ToString("D"),
                "First valid game discovery");
        }

        var priorSameGame = await store.HasValidSessionForGameAsync(
            userId,
            session.GameId,
            session.SessionId,
            cancellationToken);
        if (!priorSameGame)
        {
            await Apply(
                XpRuleCodes.NewGameDiscovered,
                XpReferenceTypes.Game,
                session.GameId.ToString("D"),
                "New game discovered");
        }

        foreach (var categoryId in await store.GetGameCategoryIdsAsync(session.GameId, cancellationToken))
        {
            var priorCategory = await store.HasValidSessionForCategoryAsync(
                userId,
                categoryId,
                session.SessionId,
                cancellationToken);
            if (!priorCategory)
            {
                await Apply(
                    XpRuleCodes.NewGenreDiscovered,
                    XpReferenceTypes.Category,
                    categoryId.ToString("D"),
                    "New genre/category discovered");
            }
        }

        await Apply(
            XpRuleCodes.ValidGameSession,
            XpReferenceTypes.GameSession,
            session.SessionId,
            "Valid game session");

        var milestone = SessionMilestoneEvaluator.SelectHighestMilestoneRule(session.ActiveSeconds);
        if (milestone is not null)
        {
            await Apply(
                milestone,
                XpReferenceTypes.GameSession,
                session.SessionId,
                $"Session milestone {milestone}");
        }

        if (totalXp == 0)
        {
            var progress = await store.GetOrCreateProgressAsync(userId, cancellationToken);
            totalXp = progress.TotalXp;
            previousLevel ??= progress.Level;
            currentLevel ??= progress.Level;
        }

        var xpAwarded = awarded.Sum(x => x.Xp);
        return new XpAwardResult(
            true,
            xpAwarded,
            totalXp,
            xpAwarded == 0 ? lastReason ?? "NO_XP" : null,
            awarded,
            previousLevel,
            currentLevel,
            leveledUp,
            level);
    }

    public Task<XpAwardResult> ProcessFavoriteAddedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default) =>
        AwardAsync(
            new XpAwardRequest(
                userId,
                XpRuleCodes.FavoriteAdded,
                "FAVORITE_ADDED",
                XpReferenceTypes.Game,
                gameId.ToString("D"),
                catalog.ResolveAmount(XpRuleCodes.FavoriteAdded),
                "Favorite added"),
            cancellationToken);

    public Task<XpAwardResult> ProcessRatingCreatedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default) =>
        AwardAsync(
            new XpAwardRequest(
                userId,
                XpRuleCodes.RatingCreated,
                "RATING_CREATED",
                XpReferenceTypes.Game,
                gameId.ToString("D"),
                catalog.ResolveAmount(XpRuleCodes.RatingCreated),
                "Rating created"),
            cancellationToken);

    public Task<XpAwardResult> ProcessReviewCreatedAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default) =>
        AwardAsync(
            new XpAwardRequest(
                userId,
                XpRuleCodes.ReviewCreated,
                "REVIEW_CREATED",
                XpReferenceTypes.Game,
                gameId.ToString("D"),
                catalog.ResolveAmount(XpRuleCodes.ReviewCreated),
                "Review created"),
            cancellationToken);

    public Task<XpAwardResult> AdminAdjustAsync(
        Guid userId,
        Guid adminId,
        int xpAmount,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (xpAmount == 0)
        {
            return Task.FromResult(new XpAwardResult(false, 0, 0, "ZERO_AMOUNT", []));
        }

        return AwardAsync(
            new XpAwardRequest(
                userId,
                XpRuleCodes.AdminAdjustment,
                "ADMIN_ADJUSTMENT",
                XpReferenceTypes.Admin,
                Guid.NewGuid().ToString("D"),
                xpAmount,
                string.IsNullOrWhiteSpace(description) ? "Admin adjustment" : description.Trim(),
                AdminId: adminId),
            cancellationToken);
    }

    public async Task<XpAwardResult> ReverseAsync(
        Guid transactionId,
        Guid adminId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var original = await store.GetTransactionByIdAsync(transactionId, cancellationToken);
        if (original is null)
        {
            return new XpAwardResult(false, 0, 0, "TRANSACTION_NOT_FOUND", []);
        }

        if (original.XpAmount == 0)
        {
            return new XpAwardResult(false, 0, 0, "ZERO_AMOUNT", []);
        }

        return await AwardAsync(
            new XpAwardRequest(
                original.UserId,
                XpRuleCodes.XpReversal,
                "XP_REVERSAL",
                XpReferenceTypes.Reversal,
                original.TransactionId.ToString("D"),
                -original.XpAmount,
                reason ?? $"Reversal of {original.RuleCode}",
                AdminId: adminId,
                ReversalOfTransactionId: original.Id),
            cancellationToken);
    }

    public async Task<UserXpSummary> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var progress = await store.GetOrCreateProgressAsync(userId, cancellationToken);
        var recent = await store.GetTransactionsAsync(userId, 10, 0, null, null, null, cancellationToken);
        return new UserXpSummary(progress.TotalXp, progress.Level, progress.CurrentLevelXp, recent);
    }

    public Task<IReadOnlyList<XpTransactionDto>> GetHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        string? ruleCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default) =>
        store.GetTransactionsAsync(userId, limit, offset, ruleCode, from, to, cancellationToken);

    private async Task NotifyLeaderboardsAsync(
        XpAwardRequest request,
        int xpAwarded,
        CancellationToken cancellationToken)
    {
        foreach (var sink in leaderboardSinks)
        {
            try
            {
                await sink.OnXpTransactionAsync(
                    request.UserId,
                    request.RuleCode,
                    request.EventType,
                    xpAwarded,
                    DateTimeOffset.UtcNow,
                    request.ReferenceType,
                    request.ReferenceId,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Leaderboard sink failed for user {UserId}", request.UserId);
            }
        }
    }

    private async Task EmitLevelUpAsync(
        Guid userId,
        int oldLevel,
        int newLevel,
        long totalXp,
        CancellationToken cancellationToken)
    {
        try
        {
            await analytics.TrackAsync(
                new AnalyticsEventIngestRequest(
                    Guid.NewGuid(),
                    AnalyticsEventTypes.LevelUp,
                    null,
                    null,
                    null,
                    "server",
                    "api",
                    null,
                    null,
                    null,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["oldLevel"] = oldLevel,
                        ["newLevel"] = newLevel,
                        ["totalXp"] = totalXp
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
            logger.LogWarning(ex, "Failed to emit LEVEL_UP analytics for user {UserId}", userId);
        }

        try
        {
            foreach (var sink in achievementSinks)
            {
                await sink.OnLevelUpAsync(userId, newLevel, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify achievement sinks for LEVEL_UP user {UserId}", userId);
        }
    }
}

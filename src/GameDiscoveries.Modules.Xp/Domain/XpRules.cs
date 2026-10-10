using GameDiscoveries.Modules.Xp.Options;

namespace GameDiscoveries.Modules.Xp.Domain;

public static class XpRuleCodes
{
    public const string FirstGameDiscovery = "FIRST_GAME_DISCOVERY";
    public const string ValidGameSession = "VALID_GAME_SESSION";
    public const string SessionMilestone2M = "SESSION_MILESTONE_2M";
    public const string SessionMilestone5M = "SESSION_MILESTONE_5M";
    public const string SessionMilestone10M = "SESSION_MILESTONE_10M";
    public const string NewGameDiscovered = "NEW_GAME_DISCOVERED";
    public const string NewGenreDiscovered = "NEW_GENRE_DISCOVERED";
    public const string FavoriteAdded = "FAVORITE_ADDED";
    public const string RatingCreated = "RATING_CREATED";
    public const string ReviewCreated = "REVIEW_CREATED";
    public const string DailyMissionCompleted = "DAILY_MISSION_COMPLETED";
    public const string WeeklyChallengeCompleted = "WEEKLY_CHALLENGE_COMPLETED";
    public const string StreakMilestone = "STREAK_MILESTONE";
    public const string AchievementUnlock = "ACHIEVEMENT_UNLOCK";
    public const string QualifiedReferral = "QUALIFIED_REFERRAL";
    public const string RewardedAdWatched = "REWARDED_AD_WATCHED";
    public const string AdminAdjustment = "ADMIN_ADJUSTMENT";
    public const string XpReversal = "XP_REVERSAL";
}

public static class XpReferenceTypes
{
    public const string GameSession = "GAME_SESSION";
    public const string Game = "GAME";
    public const string Category = "CATEGORY";
    public const string User = "USER";
    public const string Admin = "ADMIN";
    public const string Reversal = "REVERSAL";
    public const string UserMission = "USER_MISSION";
    public const string Achievement = "ACHIEVEMENT";
    public const string AdReward = "AD_REWARD";
}

public sealed record XpRuleDefinition(
    string RuleCode,
    string EventType,
    int XpAmount,
    bool IsUnique,
    bool IsActive,
    string Description);

public interface IXpRuleCatalog
{
    IReadOnlyList<XpRuleDefinition> GetActiveRules();

    XpRuleDefinition? Get(string ruleCode);

    int ResolveAmount(string ruleCode);
}

public sealed class XpRuleCatalog(Microsoft.Extensions.Options.IOptions<XpOptions> options) : IXpRuleCatalog
{
    public IReadOnlyList<XpRuleDefinition> GetActiveRules()
    {
        var o = options.Value;
        return
        [
            new(XpRuleCodes.FirstGameDiscovery, "GAME_SESSION_END", o.FirstGameDiscoveryXp, true, true, "First valid game discovery"),
            new(XpRuleCodes.ValidGameSession, "GAME_SESSION_END", o.ValidGameSessionXp, true, true, "Valid game session (>=30s active)"),
            new(XpRuleCodes.SessionMilestone2M, "GAME_SESSION_END", o.SessionMilestone2MinutesXp, true, true, "2 minute session milestone"),
            new(XpRuleCodes.SessionMilestone5M, "GAME_SESSION_END", o.SessionMilestone5MinutesXp, true, true, "5 minute session milestone"),
            new(XpRuleCodes.SessionMilestone10M, "GAME_SESSION_END", o.SessionMilestone10MinutesXp, true, true, "10 minute session milestone"),
            new(XpRuleCodes.NewGameDiscovered, "GAME_SESSION_END", o.NewGameXp, true, true, "First valid session for a game"),
            new(XpRuleCodes.NewGenreDiscovered, "GAME_SESSION_END", o.NewGenreXp, true, true, "First valid session for a category/genre"),
            new(XpRuleCodes.FavoriteAdded, "FAVORITE_ADDED", o.FavoriteXp, true, true, "Favorite a game"),
            new(XpRuleCodes.RatingCreated, "RATING_CREATED", o.RatingXp, true, true, "Create a rating"),
            new(XpRuleCodes.ReviewCreated, "REVIEW_CREATED", o.ReviewXp, true, true, "Create a review"),
            new(XpRuleCodes.DailyMissionCompleted, "DAILY_MISSION_COMPLETED", o.DailyMissionXp, true, true, "Daily mission completed"),
            new(XpRuleCodes.WeeklyChallengeCompleted, "WEEKLY_CHALLENGE_COMPLETED", o.WeeklyChallengeXp, true, true, "Weekly challenge completed"),
            new(XpRuleCodes.StreakMilestone, "STREAK_MILESTONE", 0, true, true, "Streak milestone reward"),
            new(XpRuleCodes.AchievementUnlock, "ACHIEVEMENT_UNLOCK", 0, true, true, "Achievement unlock reward"),
            new(XpRuleCodes.QualifiedReferral, "QUALIFIED_REFERRAL", o.QualifiedReferralXp, true, false, "Qualified referral (future)"),
            new(XpRuleCodes.RewardedAdWatched, "REWARDED_AD_COMPLETED", o.RewardedAdXp, true, true, "Rewarded video watched (AdMob server-side verified)"),
            new(XpRuleCodes.AdminAdjustment, "ADMIN_ADJUSTMENT", 0, false, true, "Admin XP adjustment"),
            new(XpRuleCodes.XpReversal, "XP_REVERSAL", 0, false, true, "XP reversal / compensating transaction")
        ];
    }

    public XpRuleDefinition? Get(string ruleCode) =>
        GetActiveRules().FirstOrDefault(r =>
            string.Equals(r.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase));

    public int ResolveAmount(string ruleCode) => Get(ruleCode)?.XpAmount ?? 0;
}

/// <summary>
/// Pure milestone selection: only the highest milestone is awarded.
/// </summary>
public static class SessionMilestoneEvaluator
{
    public static string? SelectHighestMilestoneRule(int activeSeconds) =>
        activeSeconds switch
        {
            >= 600 => XpRuleCodes.SessionMilestone10M,
            >= 300 => XpRuleCodes.SessionMilestone5M,
            >= 120 => XpRuleCodes.SessionMilestone2M,
            _ => null
        };
}

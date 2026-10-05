namespace GameDiscoveries.Modules.Analytics.Domain;

/// <summary>
/// Strongly typed Phase 01 event catalog (UPPER_SNAKE).
/// Legacy snake_case aliases are accepted and normalized to these values.
/// </summary>
public static class AnalyticsEventTypes
{
    // Application
    public const string AppOpen = "APP_OPEN";
    public const string AppInstall = "APP_INSTALL";
    public const string AppUpdate = "APP_UPDATE";

    // Authentication
    public const string SignUp = "SIGN_UP";
    public const string Login = "LOGIN";
    public const string Logout = "LOGOUT";

    // Discovery
    public const string HomeView = "HOME_VIEW";
    public const string Search = "SEARCH";
    public const string SearchResultClick = "SEARCH_RESULT_CLICK";
    public const string CategoryView = "CATEGORY_VIEW";
    public const string TagView = "TAG_VIEW";
    public const string GameView = "GAME_VIEW";

    // Game
    public const string GameStart = "GAME_START";
    public const string GameSessionStart = "GAME_SESSION_START";
    public const string GameSessionHeartbeat = "GAME_SESSION_HEARTBEAT";
    public const string GameSessionPause = "GAME_SESSION_PAUSE";
    public const string GameSessionResume = "GAME_SESSION_RESUME";
    public const string GameSessionEnd = "GAME_SESSION_END";

    // Engagement
    public const string FavoriteAdded = "FAVORITE_ADDED";
    public const string FavoriteRemoved = "FAVORITE_REMOVED";
    public const string RatingCreated = "RATING_CREATED";
    public const string ReviewCreated = "REVIEW_CREATED";
    public const string GameShared = "GAME_SHARED";

    // Navigation
    public const string PageView = "PAGE_VIEW";
    public const string ScreenView = "SCREEN_VIEW";

    // Social / Growth
    public const string ReferralClick = "REFERRAL_CLICK";
    public const string ReferralSignup = "REFERRAL_SIGNUP";

    // Gamification
    public const string MissionView = "MISSION_VIEW";
    public const string AchievementView = "ACHIEVEMENT_VIEW";
    public const string LeaderboardView = "LEADERBOARD_VIEW";
    public const string RewardView = "REWARD_VIEW";
    public const string LevelUp = "LEVEL_UP";
    public const string ProfileView = "PROFILE_VIEW";
    public const string AdminXpAdjustment = "ADMIN_XP_ADJUSTMENT";
    public const string DailyMissionCompleted = "DAILY_MISSION_COMPLETED";
    public const string WeeklyChallengeCompleted = "WEEKLY_CHALLENGE_COMPLETED";
    public const string MissionExpired = "MISSION_EXPIRED";
    public const string MissionProgress = "MISSION_PROGRESS";
    public const string AchievementUnlocked = "ACHIEVEMENT_UNLOCKED";
    public const string AchievementProgress = "ACHIEVEMENT_PROGRESS";

    // Existing product events retained for continuity
    public const string RecommendationImpression = "RECOMMENDATION_IMPRESSION";
    public const string RecommendationClicked = "RECOMMENDATION_CLICKED";
    public const string RecommendationStarted = "RECOMMENDATION_STARTED";
    public const string RecommendationCompleted = "RECOMMENDATION_COMPLETED";
    public const string RecommendationDismissed = "RECOMMENDATION_DISMISSED";
    public const string CommunityViewed = "COMMUNITY_VIEWED";
    public const string CommunityPostCreated = "COMMUNITY_POST_CREATED";
    public const string CommunityPostViewed = "COMMUNITY_POST_VIEWED";
    public const string CommunityCommentCreated = "COMMUNITY_COMMENT_CREATED";
    public const string CommunityReactionAdded = "COMMUNITY_REACTION_ADDED";
    public const string CommunityUserFollowed = "COMMUNITY_USER_FOLLOWED";
    public const string CommunityReportCreated = "COMMUNITY_REPORT_CREATED";
    public const string PwaInstall = "PWA_INSTALL";
    public const string PwaLaunch = "PWA_LAUNCH";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AppOpen, AppInstall, AppUpdate,
        SignUp, Login, Logout,
        HomeView, Search, SearchResultClick, CategoryView, TagView, GameView,
        GameStart, GameSessionStart, GameSessionHeartbeat, GameSessionPause, GameSessionResume, GameSessionEnd,
        FavoriteAdded, FavoriteRemoved, RatingCreated, ReviewCreated, GameShared,
        PageView, ScreenView,
        ReferralClick, ReferralSignup,
        MissionView, AchievementView, LeaderboardView, RewardView, LevelUp, ProfileView, AdminXpAdjustment,
        DailyMissionCompleted, WeeklyChallengeCompleted, MissionExpired, MissionProgress, AchievementUnlocked, AchievementProgress,
        RecommendationImpression, RecommendationClicked, RecommendationStarted, RecommendationCompleted, RecommendationDismissed,
        CommunityViewed, CommunityPostCreated, CommunityPostViewed, CommunityCommentCreated, CommunityReactionAdded,
        CommunityUserFollowed, CommunityReportCreated,
        PwaInstall, PwaLaunch
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["page_view"] = PageView,
        ["home_view"] = HomeView,
        ["search"] = Search,
        ["search_result_click"] = SearchResultClick,
        ["category_view"] = CategoryView,
        ["tag_view"] = TagView,
        ["game_view"] = GameView,
        ["game_viewed"] = GameView,
        ["game_impression"] = GameView,
        ["game_click"] = SearchResultClick,
        ["game_start"] = GameStart,
        ["game_started"] = GameStart,
        ["game_play_clicked"] = GameStart,
        ["game_session_start"] = GameSessionStart,
        ["game_session_heartbeat"] = GameSessionHeartbeat,
        ["game_session_pause"] = GameSessionPause,
        ["game_session_resume"] = GameSessionResume,
        ["game_session_end"] = GameSessionEnd,
        ["game_exit"] = GameSessionEnd,
        ["game_exited"] = GameSessionEnd,
        ["game_completed"] = GameSessionEnd,
        ["favorite_added"] = FavoriteAdded,
        ["favorite_removed"] = FavoriteRemoved,
        ["rating_created"] = RatingCreated,
        ["review_created"] = ReviewCreated,
        ["community_review_created"] = ReviewCreated,
        ["community_review_updated"] = ReviewCreated,
        ["game_shared"] = GameShared,
        ["share_clicked"] = GameShared,
        ["share_completed"] = GameShared,
        ["community_game_shared"] = GameShared,
        ["screen_view"] = ScreenView,
        ["referral_click"] = ReferralClick,
        ["referral_signup"] = ReferralSignup,
        ["mission_view"] = MissionView,
        ["achievement_unlocked"] = AchievementUnlocked,
        ["achievement_progress"] = AchievementProgress,
        ["achievement_view"] = AchievementView,
        ["leaderboard_view"] = LeaderboardView,
        ["community_leaderboard_viewed"] = LeaderboardView,
        ["reward_view"] = RewardView,
        ["app_open"] = AppOpen,
        ["app_install"] = AppInstall,
        ["app_update"] = AppUpdate,
        ["pwa_installed"] = AppInstall,
        ["pwa_install"] = AppInstall,
        ["pwa_install_prompt_accepted"] = AppInstall,
        ["pwa_launch"] = AppOpen,
        ["sign_up"] = SignUp,
        ["signup"] = SignUp,
        ["auth_register_succeeded"] = SignUp,
        ["login"] = Login,
        ["auth_login_succeeded"] = Login,
        ["logout"] = Logout,
        ["auth_logout"] = Logout,
        ["recommendation_impression"] = RecommendationImpression,
        ["recommendation_clicked"] = RecommendationClicked,
        ["recommendation_started"] = RecommendationStarted,
        ["recommendation_completed"] = RecommendationCompleted,
        ["recommendation_dismissed"] = RecommendationDismissed,
        ["community_viewed"] = CommunityViewed,
        ["community_post_created"] = CommunityPostCreated,
        ["community_post_viewed"] = CommunityPostViewed,
        ["community_comment_created"] = CommunityCommentCreated,
        ["community_reaction_added"] = CommunityReactionAdded,
        ["community_user_followed"] = CommunityUserFollowed,
        ["community_report_created"] = CommunityReportCreated,
        ["organic_landing"] = PageView,
        ["return_session"] = AppOpen,
        ["favorites_viewed"] = PageView,
        ["history_viewed"] = PageView,
        ["my_games_viewed"] = PageView,
        ["auth_login_viewed"] = PageView,
        ["auth_login_submitted"] = PageView,
        ["auth_login_failed"] = PageView,
        ["auth_register_submitted"] = PageView,
        ["auth_register_failed"] = PageView,
        ["pwa_install_prompt_shown"] = PageView,
        ["pwa_install_prompt_dismissed"] = PageView,
        ["pwa_update_available"] = AppUpdate,
        ["pwa_update_accepted"] = AppUpdate,
        ["pwa_offline"] = PageView,
        ["pwa_online"] = PageView,
        ["community_post_clicked"] = CommunityPostViewed,
        ["community_user_unfollowed"] = CommunityUserFollowed,
        ["community_achievement_unlocked"] = AchievementUnlocked,
        ["community_challenge_started"] = MissionView,
        ["community_challenge_completed"] = MissionView,
        ["community_joined"] = CommunityViewed,
        ["history_game_clicked"] = GameView,
        ["my_games_game_clicked"] = GameView,
        ["install_prompt_shown"] = PageView
    };

    public static bool TryNormalize(string? raw, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (All.Contains(trimmed))
        {
            canonical = All.First(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        if (Aliases.TryGetValue(trimmed, out var mapped))
        {
            canonical = mapped;
            return true;
        }

        return false;
    }
}

public static class AnalyticsSources
{
    public const string Web = "WEB";
    public const string Mobile = "MOBILE";
    public const string Game = "GAME";
    public const string Api = "API";
    public const string Admin = "ADMIN";
    public const string System = "SYSTEM";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Web, Mobile, Game, Api, Admin, System
    };

    public static string Normalize(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && All.Contains(raw)
            ? All.First(x => string.Equals(x, raw, StringComparison.OrdinalIgnoreCase))
            : Web;
}

public static class AnalyticsPlatforms
{
    public const string Web = "WEB";
    public const string Android = "ANDROID";
    public const string Ios = "IOS";
    public const string Desktop = "DESKTOP";
    public const string Unknown = "UNKNOWN";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Web, Android, Ios, Desktop, Unknown
    };

    public static string Normalize(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && All.Contains(raw)
            ? All.First(x => string.Equals(x, raw, StringComparison.OrdinalIgnoreCase))
            : Web;
}

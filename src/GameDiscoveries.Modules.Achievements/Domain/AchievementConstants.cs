namespace GameDiscoveries.Modules.Achievements.Domain;

public static class AchievementCategories
{
    public const string Discovery = "DISCOVERY";
    public const string Gameplay = "GAMEPLAY";
    public const string Exploration = "EXPLORATION";
    public const string Social = "SOCIAL";
    public const string Collection = "COLLECTION";
    public const string Streak = "STREAK";
    public const string Progression = "PROGRESSION";
    public const string Special = "SPECIAL";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Discovery, Gameplay, Exploration, Social, Collection, Streak, Progression, Special
    };
}

public static class AchievementDifficulties
{
    public const string Easy = "EASY";
    public const string Medium = "MEDIUM";
    public const string Hard = "HARD";
    public const string Epic = "EPIC";
    public const string Legendary = "LEGENDARY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Easy, Medium, Hard, Epic, Legendary
    };
}

public static class AchievementRequirementTypes
{
    public const string FirstGamePlayed = "FIRST_GAME_PLAYED";
    public const string GamesPlayed = "GAMES_PLAYED";
    public const string UniqueGamesPlayed = "UNIQUE_GAMES_PLAYED";
    public const string GamesDiscovered = "GAMES_DISCOVERED";
    public const string UniqueGenresPlayed = "UNIQUE_GENRES_PLAYED";
    public const string FavoritesCount = "FAVORITES_COUNT";
    public const string RatingsCount = "RATINGS_COUNT";
    public const string ReviewsCount = "REVIEWS_COUNT";
    public const string TotalActiveTime = "TOTAL_ACTIVE_TIME";
    public const string ValidSessionsCount = "VALID_SESSIONS_COUNT";
    public const string StreakDays = "STREAK_DAYS";
    public const string LongestStreak = "LONGEST_STREAK";
    public const string CurrentLevel = "CURRENT_LEVEL";
    public const string TotalXp = "TOTAL_XP";
    public const string DailyMissionsCompleted = "DAILY_MISSIONS_COMPLETED";
    public const string WeeklyChallengesCompleted = "WEEKLY_CHALLENGES_COMPLETED";
    public const string AchievementCount = "ACHIEVEMENT_COUNT";
    public const string SpecialCondition = "SPECIAL_CONDITION";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        FirstGamePlayed, GamesPlayed, UniqueGamesPlayed, GamesDiscovered, UniqueGenresPlayed,
        FavoritesCount, RatingsCount, ReviewsCount, TotalActiveTime, ValidSessionsCount,
        StreakDays, LongestStreak, CurrentLevel, TotalXp, DailyMissionsCompleted,
        WeeklyChallengesCompleted, AchievementCount, SpecialCondition
    };
}

public static class AchievementHistoryEventTypes
{
    public const string Unlocked = "UNLOCKED";
    public const string Granted = "GRANTED";
    public const string Revoked = "REVOKED";
    public const string Activated = "ACTIVATED";
    public const string Deactivated = "DEACTIVATED";
    public const string Created = "CREATED";
    public const string Updated = "UPDATED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Unlocked, Granted, Revoked, Activated, Deactivated, Created, Updated
    };
}

public static class AchievementTriggers
{
    public const string Any = "ANY";
    public const string ValidSessionEnded = "VALID_SESSION_ENDED";
    public const string FavoriteAdded = "FAVORITE_ADDED";
    public const string RatingCreated = "RATING_CREATED";
    public const string ReviewCreated = "REVIEW_CREATED";
    public const string LevelUp = "LEVEL_UP";
    public const string MissionCompleted = "MISSION_COMPLETED";
    public const string StreakProgress = "STREAK_PROGRESS";
}

public static class AchievementTriggerRequirements
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Mapping =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [AchievementTriggers.ValidSessionEnded] = Set(
                AchievementRequirementTypes.FirstGamePlayed,
                AchievementRequirementTypes.GamesPlayed,
                AchievementRequirementTypes.UniqueGamesPlayed,
                AchievementRequirementTypes.GamesDiscovered,
                AchievementRequirementTypes.UniqueGenresPlayed,
                AchievementRequirementTypes.TotalActiveTime,
                AchievementRequirementTypes.ValidSessionsCount),
            [AchievementTriggers.FavoriteAdded] = Set(AchievementRequirementTypes.FavoritesCount),
            [AchievementTriggers.RatingCreated] = Set(AchievementRequirementTypes.RatingsCount),
            [AchievementTriggers.ReviewCreated] = Set(AchievementRequirementTypes.ReviewsCount),
            [AchievementTriggers.LevelUp] = Set(AchievementRequirementTypes.CurrentLevel, AchievementRequirementTypes.TotalXp),
            [AchievementTriggers.MissionCompleted] = Set(
                AchievementRequirementTypes.DailyMissionsCompleted,
                AchievementRequirementTypes.WeeklyChallengesCompleted),
            [AchievementTriggers.StreakProgress] = Set(
                AchievementRequirementTypes.StreakDays,
                AchievementRequirementTypes.LongestStreak),
            [AchievementTriggers.Any] = AchievementRequirementTypes.All
        };

    public static IReadOnlySet<string> ForTrigger(string? trigger) =>
        !string.IsNullOrWhiteSpace(trigger) && Mapping.TryGetValue(trigger, out var requirements)
            ? requirements
            : AchievementRequirementTypes.All;

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}

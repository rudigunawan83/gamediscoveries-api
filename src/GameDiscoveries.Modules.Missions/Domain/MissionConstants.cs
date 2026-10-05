namespace GameDiscoveries.Modules.Missions.Domain;

public static class MissionTypes
{
    public const string Daily = "DAILY";
    public const string Weekly = "WEEKLY";
}

public static class MissionStatuses
{
    public const string Active = "ACTIVE";
    public const string Completed = "COMPLETED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}

public static class MissionDifficulties
{
    public const string Easy = "EASY";
    public const string Medium = "MEDIUM";
    public const string Hard = "HARD";
}

public static class MissionRequirementTypes
{
    public const string UniqueGamesPlayed = "UNIQUE_GAMES_PLAYED";
    public const string NewGameDiscovered = "NEW_GAME_DISCOVERED";
    public const string NewGenreDiscovered = "NEW_GENRE_DISCOVERED";
    public const string ActiveTimeSeconds = "ACTIVE_TIME_SECONDS";
    public const string FavoritesAdded = "FAVORITES_ADDED";
    public const string ActiveDays = "ACTIVE_DAYS";
    public const string UniqueGenresPlayed = "UNIQUE_GENRES_PLAYED";
    public const string TotalValidSessions = "TOTAL_VALID_SESSIONS";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        UniqueGamesPlayed, NewGameDiscovered, NewGenreDiscovered, ActiveTimeSeconds,
        FavoritesAdded, ActiveDays, UniqueGenresPlayed, TotalValidSessions
    };
}

public static class MissionCodes
{
    public const string Play2Games = "PLAY_2_GAMES";
    public const string DiscoverNewGame = "DISCOVER_NEW_GAME";
    public const string ExploreNewGenre = "EXPLORE_NEW_GENRE";
    public const string Play10Minutes = "PLAY_10_MINUTES";
    public const string FavoriteAGame = "FAVORITE_A_GAME";
    public const string Play5Days = "PLAY_5_DAYS";
    public const string Play5Games = "PLAY_5_GAMES";
    public const string Explore5Genres = "EXPLORE_5_GENRES";
    public const string Play60Minutes = "PLAY_60_MINUTES";
    public const string Discover10Games = "DISCOVER_10_GAMES";
}

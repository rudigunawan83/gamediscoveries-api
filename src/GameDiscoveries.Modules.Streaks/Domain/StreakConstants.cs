namespace GameDiscoveries.Modules.Streaks.Domain;

public static class StreakStatuses
{
    public const string Active = "ACTIVE";
    public const string AtRisk = "AT_RISK";
    public const string Broken = "BROKEN";
    public const string Frozen = "FROZEN";
}

public static class StreakEventTypes
{
    public const string Started = "STREAK_STARTED";
    public const string Continued = "STREAK_CONTINUED";
    public const string Milestone = "STREAK_MILESTONE";
    public const string Frozen = "STREAK_FROZEN";
    public const string Broken = "STREAK_BROKEN";
    public const string Recovered = "STREAK_RECOVERED";
    public const string AdminReset = "ADMIN_STREAK_RESET";
    public const string AdminFreezeGranted = "ADMIN_FREEZE_GRANTED";
    public const string AdminFreezeRemoved = "ADMIN_FREEZE_REMOVED";
}

public static class StreakXpRuleCodes
{
    public const string StreakMilestone = "STREAK_MILESTONE";
}

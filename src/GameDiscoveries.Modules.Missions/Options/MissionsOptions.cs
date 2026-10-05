namespace GameDiscoveries.Modules.Missions.Options;

public sealed class MissionsOptions
{
    public const string SectionName = "Missions";

    public bool Enabled { get; set; } = true;

    /// <summary>IANA timezone for mission day/week boundaries. Default Asia/Jakarta.</summary>
    public string TimeZone { get; set; } = "Asia/Jakarta";

    public int DailyMissionCount { get; set; } = 3;

    public int WeeklyChallengeCount { get; set; } = 2;

    /// <summary>Users with TotalXp below this prefer easier daily missions.</summary>
    public int NewUserXpThreshold { get; set; } = 100;
}

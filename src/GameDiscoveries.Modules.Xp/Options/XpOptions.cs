namespace GameDiscoveries.Modules.Xp.Options;

public sealed class XpOptions
{
    public const string SectionName = "Xp";

    public bool Enabled { get; set; } = true;

    public int DailyXpCap { get; set; } = 500;

    public int ValidGameSessionXp { get; set; } = 10;

    public int FirstGameDiscoveryXp { get; set; } = 20;

    public int NewGameXp { get; set; } = 15;

    public int NewGenreXp { get; set; } = 20;

    public int FavoriteXp { get; set; } = 10;

    public int RatingXp { get; set; } = 10;

    public int ReviewXp { get; set; } = 30;

    public int SessionMilestone2MinutesXp { get; set; } = 10;

    public int SessionMilestone5MinutesXp { get; set; } = 20;

    public int SessionMilestone10MinutesXp { get; set; } = 30;

    public int DailyMissionXp { get; set; } = 50;

    public int WeeklyChallengeXp { get; set; } = 150;

    public int QualifiedReferralXp { get; set; } = 100;
}

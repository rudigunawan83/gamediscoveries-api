namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

/// <summary>
/// One trend bucket. Xp is the sum of positive awards; players earned XP in the bucket;
/// missions and achievements are counted when they were completed or unlocked.
/// </summary>
public sealed record GamificationPoint(
    DateTimeOffset Bucket,
    long Xp,
    long Players,
    long DailyMissions,
    long WeeklyMissions,
    long Achievements);

public sealed record GamificationShare(string Key, long Count);

/// <summary>
/// Missions that were open at any time in the range. CompletionRate is completed
/// over all of them, or null when none were assigned.
/// </summary>
public sealed record GamificationMissionRate(
    long Completed,
    long InProgress,
    long NotStarted,
    long Expired,
    double? CompletionRate);

public sealed record GamificationTopPlayer(
    Guid UserId,
    string Player,
    int Level,
    long Xp,
    double? ChangePercent);

public sealed record GamificationTopMission(
    string Title,
    string Type,
    long Completed,
    long Assigned,
    double? CompletionRate);

public sealed record GamificationTopAchievement(string Title, string Difficulty, long Unlocks);

public sealed record GamificationXpEntry(
    DateTimeOffset At,
    Guid UserId,
    string Player,
    string Description,
    string Source,
    int Xp);

public sealed record GamificationRecentAchievement(
    DateTimeOffset At,
    Guid UserId,
    string Player,
    string Achievement,
    string Difficulty);

public sealed record GamificationRecentMission(
    DateTimeOffset At,
    Guid UserId,
    string Player,
    string Mission,
    string Type,
    int RewardXp);

/// <summary>
/// Seeded demo accounts are excluded everywhere. Levels and streaks are current
/// snapshots, so ActiveStreaks has no change percent.
/// </summary>
public sealed record MonitoringGamificationResponse(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    MonitoringCount XpAwarded,
    MonitoringCount ActivePlayers,
    MonitoringCount MissionCompletions,
    MonitoringCount AchievementsUnlocked,
    MonitoringCount ActiveStreaks,
    IReadOnlyList<GamificationPoint> Trend,
    IReadOnlyList<GamificationShare> AchievementsByDifficulty,
    IReadOnlyList<GamificationShare> Levels,
    IReadOnlyList<GamificationShare> Streaks,
    GamificationMissionRate MissionRate,
    IReadOnlyList<GamificationTopPlayer> TopPlayers,
    IReadOnlyList<GamificationTopMission> TopMissions,
    IReadOnlyList<GamificationTopAchievement> TopAchievements,
    IReadOnlyList<GamificationXpEntry> RecentXp,
    IReadOnlyList<GamificationRecentAchievement> RecentAchievements,
    IReadOnlyList<GamificationRecentMission> RecentMissions);

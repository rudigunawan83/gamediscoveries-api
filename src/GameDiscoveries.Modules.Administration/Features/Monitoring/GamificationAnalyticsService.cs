using System.Data.Common;
using Dapper;
using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.BuildingBlocks.Database;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public interface IGamificationAnalyticsService
{
    Task<MonitoringGamificationResponse> GetGamificationAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default);
}

public static class GamificationBuckets
{
    public static readonly IReadOnlyList<string> Difficulties = ["EASY", "MEDIUM", "HARD", "EPIC", "LEGENDARY"];

    public static readonly IReadOnlyList<string> LevelKeys =
        ["1_5", "6_10", "11_15", "16_20", "21_30", "31_50", "51_plus"];

    public static readonly IReadOnlyList<string> StreakKeys = ["1_3", "4_7", "8_14", "15_30", "31_plus"];

    public static string Level(int level) => level switch
    {
        <= 5 => "1_5",
        <= 10 => "6_10",
        <= 15 => "11_15",
        <= 20 => "16_20",
        <= 30 => "21_30",
        <= 50 => "31_50",
        _ => "51_plus"
    };

    public static string Streak(int days) => days switch
    {
        <= 3 => "1_3",
        <= 7 => "4_7",
        <= 14 => "8_14",
        <= 30 => "15_30",
        _ => "31_plus"
    };

    /// <summary>Totals per key in display order; keys without rows are zero.</summary>
    public static IReadOnlyList<GamificationShare> Ordered(
        IReadOnlyList<string> keys,
        IEnumerable<(string Key, long Count)> rows)
    {
        var totals = rows.GroupBy(row => row.Key).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
        return keys.Select(key => new GamificationShare(key, totals.GetValueOrDefault(key))).ToList();
    }
}

public sealed class GamificationAnalyticsService(
    IDbConnectionFactory connectionFactory,
    ICacheService cache) : IGamificationAnalyticsService
{
    private const int TopLimit = 5;
    private const int RecentLimit = 6;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<MonitoringGamificationResponse> GetGamificationAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"monitoring:gamification:{window.Key}";
        if (!bypassCache)
        {
            var cached = await cache.GetAsync<MonitoringGamificationResponse>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var gamification = await LoadAsync(window, cancellationToken);
        await cache.SetAsync(cacheKey, gamification, CacheTtl, cancellationToken);
        return gamification;
    }

    private async Task<MonitoringGamificationResponse> LoadAsync(
        MonitoringWindow window,
        CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)await connectionFactory.CreateConnectionAsync(cancellationToken);
        var previous = window.Previous;
        var args = new { window.From, window.To, PrevFrom = previous.From, Limit = TopLimit, Recent = RecentLimit };
        var previousArgs = new { From = previous.From, To = previous.To };

        var totals = await connection.QuerySingleAsync<TotalsRow>(new CommandDefinition(
            TotalsSql, args, cancellationToken: cancellationToken));
        var before = await connection.QuerySingleAsync<TotalsRow>(new CommandDefinition(
            TotalsSql, previousArgs, cancellationToken: cancellationToken));

        var trend = (await connection.QueryAsync<TrendRow>(new CommandDefinition(
            window.Hourly ? HourlyTrendSql : DailyTrendSql,
            args,
            cancellationToken: cancellationToken))).ToList();

        var difficulties = (await connection.QueryAsync<KeyRow>(new CommandDefinition(
            AchievementDifficultySql, args, cancellationToken: cancellationToken))).ToList();

        var levels = (await connection.QueryAsync<ValueRow>(new CommandDefinition(
            LevelsSql, cancellationToken: cancellationToken))).ToList();

        var streaks = (await connection.QueryAsync<ValueRow>(new CommandDefinition(
            StreaksSql, cancellationToken: cancellationToken))).ToList();

        var missionRate = await connection.QuerySingleAsync<MissionRateRow>(new CommandDefinition(
            MissionRateSql, args, cancellationToken: cancellationToken));

        var topPlayers = (await connection.QueryAsync<TopPlayerRow>(new CommandDefinition(
            TopPlayersSql, args, cancellationToken: cancellationToken))).ToList();

        var topMissions = (await connection.QueryAsync<TopMissionRow>(new CommandDefinition(
            TopMissionsSql, args, cancellationToken: cancellationToken))).ToList();

        var topAchievements = (await connection.QueryAsync<TopAchievementRow>(new CommandDefinition(
            TopAchievementsSql, args, cancellationToken: cancellationToken))).ToList();

        var recentXp = (await connection.QueryAsync<XpEntryRow>(new CommandDefinition(
            RecentXpSql, args, cancellationToken: cancellationToken))).ToList();

        var recentAchievements = (await connection.QueryAsync<RecentAchievementRow>(new CommandDefinition(
            RecentAchievementsSql, args, cancellationToken: cancellationToken))).ToList();

        var recentMissions = (await connection.QueryAsync<RecentMissionRow>(new CommandDefinition(
            RecentMissionsSql, args, cancellationToken: cancellationToken))).ToList();

        return new MonitoringGamificationResponse(
            window.Key,
            window.From,
            window.To,
            DateTimeOffset.UtcNow,
            Count(totals.Xp, before.Xp),
            Count(totals.Players, before.Players),
            Count(totals.Missions, before.Missions),
            Count(totals.Achievements, before.Achievements),
            new MonitoringCount(streaks.Sum(row => row.Users), null),
            trend.Select(row => new GamificationPoint(
                AsUtc(row.Bucket),
                row.Xp,
                row.Players,
                row.DailyMissions,
                row.WeeklyMissions,
                row.Achievements)).ToList(),
            GamificationBuckets.Ordered(
                GamificationBuckets.Difficulties,
                difficulties.Select(row => (row.Key, row.Count))),
            GamificationBuckets.Ordered(
                GamificationBuckets.LevelKeys,
                levels.Select(row => (GamificationBuckets.Level(row.Value), row.Users))),
            GamificationBuckets.Ordered(
                GamificationBuckets.StreakKeys,
                streaks.Select(row => (GamificationBuckets.Streak(row.Value), row.Users))),
            new GamificationMissionRate(
                missionRate.Completed,
                missionRate.InProgress,
                missionRate.NotStarted,
                missionRate.Expired,
                UserRetentionMath.Percent(
                    missionRate.Completed,
                    missionRate.Completed + missionRate.InProgress + missionRate.NotStarted + missionRate.Expired)),
            topPlayers.Select(row => new GamificationTopPlayer(
                row.UserId,
                row.Player,
                row.Level,
                row.Xp,
                MonitoringMath.ChangePercent(row.Xp, row.PreviousXp))).ToList(),
            topMissions.Select(row => new GamificationTopMission(
                row.Title,
                row.Type,
                row.Completed,
                row.Assigned,
                UserRetentionMath.Percent(row.Completed, row.Assigned))).ToList(),
            topAchievements.Select(row => new GamificationTopAchievement(row.Title, row.Difficulty, row.Unlocks)).ToList(),
            recentXp.Select(row => new GamificationXpEntry(
                row.At, row.UserId, row.Player, row.Description, row.Source, row.Xp)).ToList(),
            recentAchievements.Select(row => new GamificationRecentAchievement(
                row.At, row.UserId, row.Player, row.Achievement, row.Difficulty)).ToList(),
            recentMissions.Select(row => new GamificationRecentMission(
                row.At, row.UserId, row.Player, row.Mission, row.Type, row.RewardXp)).ToList());
    }

    private static MonitoringCount Count(long current, long previous) =>
        new(current, MonitoringMath.ChangePercent(current, previous));

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    // Accounts created by the demo seed migrations; their XP, missions, and streaks are not real activity.
    private const string DemoUserIds =
        """
        SELECT id FROM users
        WHERE lower(email) = 'demo@gamediscoveries.com'
           OR lower(email) ~ '^player[0-9]+@gamediscoveries\.com$'
        """;

    private const string PlayerName = "COALESCE(NULLIF(u.display_name, ''), NULLIF(u.username, ''), 'Player')";

    private const string TotalsSql =
        $"""
        SELECT (SELECT COALESCE(SUM(xp_amount) FILTER (WHERE xp_amount > 0), 0) FROM xp_transactions
                WHERE created_at >= @From AND created_at < @To
                  AND user_id NOT IN ({DemoUserIds}))::bigint AS Xp,
               (SELECT COUNT(DISTINCT user_id) FROM xp_transactions
                WHERE created_at >= @From AND created_at < @To AND xp_amount > 0
                  AND user_id NOT IN ({DemoUserIds}))::bigint AS Players,
               (SELECT COUNT(*) FROM user_missions
                WHERE status = 'COMPLETED' AND completed_at >= @From AND completed_at < @To
                  AND user_id NOT IN ({DemoUserIds}))::bigint AS Missions,
               (SELECT COUNT(*) FROM user_achievement_unlocks
                WHERE revoked_at IS NULL AND unlocked_at >= @From AND unlocked_at < @To
                  AND user_id NOT IN ({DemoUserIds}))::bigint AS Achievements
        """;

    private static readonly string DailyTrendSql = TrendSql("day");

    private static readonly string HourlyTrendSql = TrendSql("hour");

    private static string TrendSql(string unit) =>
        $$"""
        WITH buckets AS (
            SELECT generate_series(
                       date_trunc('{{unit}}', @From::timestamptz),
                       date_trunc('{{unit}}', @To::timestamptz - interval '1 microsecond'),
                       interval '1 {{unit}}') AS bucket
        ),
        xp AS (
            SELECT date_trunc('{{unit}}', created_at) AS bucket,
                   SUM(xp_amount) AS xp,
                   COUNT(DISTINCT user_id) AS players
            FROM xp_transactions
            WHERE created_at >= @From AND created_at < @To AND xp_amount > 0
              AND user_id NOT IN ({{DemoUserIds}})
            GROUP BY 1
        ),
        missions AS (
            SELECT date_trunc('{{unit}}', completed_at) AS bucket,
                   COUNT(*) FILTER (WHERE type = 'DAILY') AS daily,
                   COUNT(*) FILTER (WHERE type = 'WEEKLY') AS weekly
            FROM user_missions
            WHERE status = 'COMPLETED' AND completed_at >= @From AND completed_at < @To
              AND user_id NOT IN ({{DemoUserIds}})
            GROUP BY 1
        ),
        unlocks AS (
            SELECT date_trunc('{{unit}}', unlocked_at) AS bucket, COUNT(*) AS achievements
            FROM user_achievement_unlocks
            WHERE revoked_at IS NULL AND unlocked_at >= @From AND unlocked_at < @To
              AND user_id NOT IN ({{DemoUserIds}})
            GROUP BY 1
        )
        SELECT b.bucket AS Bucket,
               COALESCE(x.xp, 0)::bigint AS Xp,
               COALESCE(x.players, 0)::bigint AS Players,
               COALESCE(m.daily, 0)::bigint AS DailyMissions,
               COALESCE(m.weekly, 0)::bigint AS WeeklyMissions,
               COALESCE(a.achievements, 0)::bigint AS Achievements
        FROM buckets b
        LEFT JOIN xp x ON x.bucket = b.bucket
        LEFT JOIN missions m ON m.bucket = b.bucket
        LEFT JOIN unlocks a ON a.bucket = b.bucket
        ORDER BY b.bucket
        """;

    private const string AchievementDifficultySql =
        $"""
        SELECT d.difficulty AS Key, COUNT(*)::bigint AS Count
        FROM user_achievement_unlocks a
        INNER JOIN achievement_definitions d ON d.id = a.achievement_definition_id
        WHERE a.revoked_at IS NULL AND a.unlocked_at >= @From AND a.unlocked_at < @To
          AND a.user_id NOT IN ({DemoUserIds})
        GROUP BY d.difficulty
        """;

    private const string LevelsSql =
        $"""
        SELECT level AS Value, COUNT(*)::bigint AS Users
        FROM user_progress
        WHERE user_id NOT IN ({DemoUserIds})
        GROUP BY level
        """;

    private const string StreaksSql =
        $"""
        SELECT current_streak AS Value, COUNT(*)::bigint AS Users
        FROM user_progress
        WHERE current_streak > 0
          AND streak_status IN ('ACTIVE', 'AT_RISK', 'FROZEN')
          AND user_id NOT IN ({DemoUserIds})
        GROUP BY current_streak
        """;

    // Missions open at any point in the range; cancelled missions are left out.
    private const string MissionRateSql =
        $"""
        SELECT COUNT(*) FILTER (WHERE status = 'COMPLETED')::bigint AS Completed,
               COUNT(*) FILTER (WHERE status = 'ACTIVE' AND progress_value > 0)::bigint AS InProgress,
               COUNT(*) FILTER (WHERE status = 'ACTIVE' AND progress_value = 0)::bigint AS NotStarted,
               COUNT(*) FILTER (WHERE status = 'EXPIRED')::bigint AS Expired
        FROM user_missions
        WHERE period_start < @To AND period_end > @From
          AND status <> 'CANCELLED'
          AND user_id NOT IN ({DemoUserIds})
        """;

    private const string TopPlayersSql =
        $"""
        WITH earned AS (
            SELECT user_id,
                   SUM(xp_amount) FILTER (WHERE created_at >= @From) AS xp,
                   COALESCE(SUM(xp_amount) FILTER (WHERE created_at < @From), 0) AS previous_xp
            FROM xp_transactions
            WHERE created_at >= @PrevFrom AND created_at < @To AND xp_amount > 0
              AND user_id NOT IN ({DemoUserIds})
            GROUP BY user_id
        )
        SELECT e.user_id AS UserId,
               {PlayerName} AS Player,
               COALESCE(p.level, 1) AS Level,
               e.xp::bigint AS Xp,
               e.previous_xp::bigint AS PreviousXp
        FROM earned e
        INNER JOIN users u ON u.id = e.user_id
        LEFT JOIN user_progress p ON p.user_id = e.user_id
        WHERE e.xp > 0
        ORDER BY e.xp DESC, e.user_id
        LIMIT @Limit
        """;

    private const string TopMissionsSql =
        $"""
        SELECT t.title AS Title,
               t.type AS Type,
               COUNT(*) FILTER (WHERE m.status = 'COMPLETED')::bigint AS Completed,
               COUNT(*)::bigint AS Assigned
        FROM user_missions m
        INNER JOIN mission_templates t ON t.id = m.mission_template_id
        WHERE m.period_start < @To AND m.period_end > @From
          AND m.status <> 'CANCELLED'
          AND m.user_id NOT IN ({DemoUserIds})
        GROUP BY t.id, t.title, t.type
        ORDER BY 3 DESC, 4 DESC, t.title
        LIMIT @Limit
        """;

    private const string TopAchievementsSql =
        $"""
        SELECT d.title AS Title, d.difficulty AS Difficulty, COUNT(*)::bigint AS Unlocks
        FROM user_achievement_unlocks a
        INNER JOIN achievement_definitions d ON d.id = a.achievement_definition_id
        WHERE a.revoked_at IS NULL AND a.unlocked_at >= @From AND a.unlocked_at < @To
          AND a.user_id NOT IN ({DemoUserIds})
        GROUP BY d.id, d.title, d.difficulty
        ORDER BY 3 DESC, d.title
        LIMIT @Limit
        """;

    private const string RecentXpSql =
        $"""
        SELECT x.created_at AS At,
               x.user_id AS UserId,
               {PlayerName} AS Player,
               x.description AS Description,
               x.event_type AS Source,
               x.xp_amount AS Xp
        FROM xp_transactions x
        INNER JOIN users u ON u.id = x.user_id
        WHERE x.user_id NOT IN ({DemoUserIds})
        ORDER BY x.created_at DESC
        LIMIT @Recent
        """;

    private const string RecentAchievementsSql =
        $"""
        SELECT a.unlocked_at AS At,
               a.user_id AS UserId,
               {PlayerName} AS Player,
               d.title AS Achievement,
               d.difficulty AS Difficulty
        FROM user_achievement_unlocks a
        INNER JOIN achievement_definitions d ON d.id = a.achievement_definition_id
        INNER JOIN users u ON u.id = a.user_id
        WHERE a.revoked_at IS NULL
          AND a.user_id NOT IN ({DemoUserIds})
        ORDER BY a.unlocked_at DESC
        LIMIT @Recent
        """;

    private const string RecentMissionsSql =
        $"""
        SELECT m.completed_at AS At,
               m.user_id AS UserId,
               {PlayerName} AS Player,
               m.title AS Mission,
               m.type AS Type,
               m.reward_xp AS RewardXp
        FROM user_missions m
        INNER JOIN users u ON u.id = m.user_id
        WHERE m.status = 'COMPLETED' AND m.completed_at IS NOT NULL
          AND m.user_id NOT IN ({DemoUserIds})
        ORDER BY m.completed_at DESC
        LIMIT @Recent
        """;

    private sealed class TotalsRow
    {
        public long Xp { get; init; }
        public long Players { get; init; }
        public long Missions { get; init; }
        public long Achievements { get; init; }
    }

    private sealed class TrendRow
    {
        public DateTime Bucket { get; init; }
        public long Xp { get; init; }
        public long Players { get; init; }
        public long DailyMissions { get; init; }
        public long WeeklyMissions { get; init; }
        public long Achievements { get; init; }
    }

    private sealed class KeyRow
    {
        public string Key { get; init; } = "";
        public long Count { get; init; }
    }

    private sealed class ValueRow
    {
        public int Value { get; init; }
        public long Users { get; init; }
    }

    private sealed class MissionRateRow
    {
        public long Completed { get; init; }
        public long InProgress { get; init; }
        public long NotStarted { get; init; }
        public long Expired { get; init; }
    }

    private sealed class TopPlayerRow
    {
        public Guid UserId { get; init; }
        public string Player { get; init; } = "";
        public int Level { get; init; }
        public long Xp { get; init; }
        public long PreviousXp { get; init; }
    }

    private sealed class TopMissionRow
    {
        public string Title { get; init; } = "";
        public string Type { get; init; } = "";
        public long Completed { get; init; }
        public long Assigned { get; init; }
    }

    private sealed class TopAchievementRow
    {
        public string Title { get; init; } = "";
        public string Difficulty { get; init; } = "";
        public long Unlocks { get; init; }
    }

    private sealed class XpEntryRow
    {
        public DateTimeOffset At { get; init; }
        public Guid UserId { get; init; }
        public string Player { get; init; } = "";
        public string Description { get; init; } = "";
        public string Source { get; init; } = "";
        public int Xp { get; init; }
    }

    private sealed class RecentAchievementRow
    {
        public DateTimeOffset At { get; init; }
        public Guid UserId { get; init; }
        public string Player { get; init; } = "";
        public string Achievement { get; init; } = "";
        public string Difficulty { get; init; } = "";
    }

    private sealed class RecentMissionRow
    {
        public DateTimeOffset At { get; init; }
        public Guid UserId { get; init; }
        public string Player { get; init; } = "";
        public string Mission { get; init; } = "";
        public string Type { get; init; } = "";
        public int RewardXp { get; init; }
    }
}

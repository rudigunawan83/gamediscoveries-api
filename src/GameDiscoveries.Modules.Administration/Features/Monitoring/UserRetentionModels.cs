namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

/// <summary>
/// A ratio or percentage with its change against the previous window.
/// Value is null when there is nothing to divide by.
/// </summary>
public sealed record MonitoringRatio(double? Value, double? ChangePercent);

/// <summary>
/// One activity bucket. Users are devices (anonymous id, else account) seen in
/// analytics events from the website and the app; new users were first seen in the bucket.
/// </summary>
public sealed record UserActivityPoint(
    DateTimeOffset Bucket,
    long ActiveUsers,
    long NewUsers,
    long ReturningUsers);

/// <summary>
/// Share of users first seen 1, 7, and 30 days before <see cref="Day"/> who were
/// active on that day. Null when no users were first seen on the cohort day.
/// </summary>
public sealed record UserRetentionPoint(DateTimeOffset Day, double? D1, double? D7, double? D30);

/// <summary>
/// Users first seen in the week starting <see cref="WeekStart"/> (Monday, UTC) and the
/// share of them active in each following week. Weeks that have not started are null.
/// </summary>
public sealed record UserCohort(DateTimeOffset WeekStart, long Users, IReadOnlyList<double?> Retention);

public sealed record UserEngagement(
    MonitoringRatio GamesPerPlayer,
    MonitoringDuration AverageSessionDuration,
    MonitoringRatio SessionsPerUser,
    MonitoringRatio PagesPerSession);

/// <summary>
/// Known users by last activity. Lapsed users were active in the last 30 days but
/// not in the range; inactive users have not been seen for 30 days or more.
/// </summary>
public sealed record UserDistribution(long New, long Returning, long Lapsed, long Inactive);

public sealed record UserTopGame(
    string Title,
    string Slug,
    string? ThumbnailUrl,
    long ActiveUsers,
    double? RepeatRate,
    double? ChangePercent);

/// <summary>Distinct users per ISO weekday (1 = Monday) and UTC hour.</summary>
public sealed record UserHeatmapCell(int Day, int Hour, long Users);

/// <summary>
/// New users grouped by the source of their first visit. ReturnRate is the share
/// that came back within 7 days, counting only users first seen at least 8 days ago.
/// </summary>
public sealed record UserAcquisitionCohort(
    string Source,
    long Users,
    double? ReturnRate,
    double? ChangePercent);

public sealed record MonitoringUsersResponse(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    MonitoringCount Dau,
    MonitoringCount Wau,
    MonitoringCount Mau,
    MonitoringRatio RetentionRate,
    MonitoringCount NewUsers,
    IReadOnlyList<UserActivityPoint> Activity,
    IReadOnlyList<UserCohort> Cohorts,
    IReadOnlyList<UserRetentionPoint> Retention,
    UserEngagement Engagement,
    UserDistribution Distribution,
    IReadOnlyList<UserTopGame> TopGames,
    IReadOnlyList<UserHeatmapCell> Heatmap,
    IReadOnlyList<UserAcquisitionCohort> Acquisition);

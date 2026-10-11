namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public sealed record MonitoringDuration(double? Seconds, double? ChangePercent);

public sealed record TrafficVisitorMix(long NewVisitors, long ReturningVisitors);

public sealed record TrafficPoint(DateTimeOffset Bucket, long Visitors, long PageViews);

public sealed record TrafficShare(string Key, long Sessions);

public sealed record TrafficPage(string Path, long Count, double? ChangePercent);

public sealed record TrafficExitPage(string Path, long Exits, double? ExitRate);

public sealed record TrafficKeyword(string Keyword, long Searches, double? ChangePercent);

public sealed record MonitoringTrafficResponse(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    MonitoringCount Visits,
    MonitoringCount PageViews,
    MonitoringCount UniqueVisitors,
    MonitoringDuration AverageSessionDuration,
    TrafficVisitorMix NewVsReturning,
    IReadOnlyList<TrafficPoint> Trend,
    IReadOnlyList<TrafficShare> Sources,
    MonitoringUnavailable Countries,
    IReadOnlyList<TrafficShare> Devices,
    IReadOnlyList<TrafficShare> Browsers,
    IReadOnlyList<TrafficPage> LandingPages,
    IReadOnlyList<TrafficExitPage> ExitPages,
    IReadOnlyList<TrafficKeyword> SearchKeywords,
    MonitoringUnavailable MobileApp);

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

/// <summary>CTR is a percentage; position is the average Google rank (lower is better).</summary>
public sealed record SeoMetricRow(string Key, long Clicks, long Impressions, double? Ctr, double? Position);

public sealed record SeoTrendPoint(DateTimeOffset Bucket, long Clicks, long Impressions, double? Ctr, double? Position);

/// <summary>Distinct queries per day by average position: 1–3, 4–10, and 11–50.</summary>
public sealed record SeoRankingPoint(DateTimeOffset Bucket, int Top3, int Top10, int Top50);

public sealed record SeoSearchPerformance(
    MonitoringCount Clicks,
    MonitoringCount Impressions,
    MonitoringRatio Ctr,
    MonitoringRatio Position,
    IReadOnlyList<SeoTrendPoint> Trend,
    IReadOnlyList<SeoRankingPoint> Rankings,
    IReadOnlyList<SeoMetricRow> Pages,
    IReadOnlyList<SeoMetricRow> Queries,
    IReadOnlyList<SeoMetricRow> Countries,
    IReadOnlyList<SeoMetricRow> Devices);

/// <summary>Metric is `lcp`, `inp`, or `cls`; rating is `good`, `needs_improvement`, or `poor`.</summary>
public sealed record SeoVital(string Metric, double? P75, double? GoodPercent, string? Rating);

public sealed record SeoCoreWebVitals(DateOnly? FirstDate, DateOnly? LastDate, IReadOnlyList<SeoVital> Metrics);

/// <summary>Severity is `high`, `medium`, or `low`.</summary>
public sealed record SeoIssue(string Kind, string Severity, string Target, string Detail);

/// <summary>
/// From and To are the Search Console dates (Pacific Time) at 12:00 UTC.
/// Each section is either filled or has an unavailable reason, never both.
/// </summary>
public sealed record MonitoringSeoResponse(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    string SiteUrl,
    SeoSearchPerformance? Search,
    MonitoringUnavailable? SearchUnavailable,
    IReadOnlyList<SearchConsoleSitemap>? Sitemaps,
    MonitoringUnavailable? SitemapsUnavailable,
    SeoCoreWebVitals? CoreWebVitals,
    MonitoringUnavailable? CoreWebVitalsUnavailable,
    IReadOnlyList<SeoIssue> Issues,
    MonitoringUnavailable IndexCoverage);

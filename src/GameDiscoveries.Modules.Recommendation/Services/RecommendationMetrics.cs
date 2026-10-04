using System.Diagnostics.Metrics;

namespace GameDiscoveries.Modules.Recommendation.Services;

public static class RecommendationMetrics
{
    public const string MeterName = "GameDiscoveries.Recommendation";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "recommendation_requests_total",
        description: "Recommendation requests");

    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "recommendation_cache_hits_total",
        description: "Recommendation cache hits");

    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "recommendation_cache_misses_total",
        description: "Recommendation cache misses");

    private static readonly Counter<long> Clicks = Meter.CreateCounter<long>(
        "recommendation_clicks_total",
        description: "Recommendation click events");

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "recommendation_generation_duration",
        unit: "ms",
        description: "Recommendation generation duration");

    private static readonly Histogram<double> Candidates = Meter.CreateHistogram<double>(
        "recommendation_candidates_count",
        description: "Candidate count before ranking");

    private static readonly Histogram<double> Results = Meter.CreateHistogram<double>(
        "recommendation_results_count",
        description: "Final recommendation count");

    public static void RecordRequest(string type, bool cacheHit, int candidates, int results, double durationMs)
    {
        var tags = new KeyValuePair<string, object?>("type", type);
        Requests.Add(1, tags);
        if (cacheHit) CacheHits.Add(1, tags);
        else CacheMisses.Add(1, tags);
        Candidates.Record(candidates, tags);
        Results.Record(results, tags);
        Duration.Record(durationMs, tags);
    }

    public static void RecordClick(string type) =>
        Clicks.Add(1, new KeyValuePair<string, object?>("type", type));
}

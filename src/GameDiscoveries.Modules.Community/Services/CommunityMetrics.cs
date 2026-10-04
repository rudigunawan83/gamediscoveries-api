using System.Diagnostics.Metrics;

namespace GameDiscoveries.Modules.Community.Services;

public static class CommunityMetrics
{
    public const string MeterName = "GameDiscoveries.Community";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> PostsCreated = Meter.CreateCounter<long>("community_posts_created");
    private static readonly Counter<long> CommentsCreated = Meter.CreateCounter<long>("community_comments_created");
    private static readonly Counter<long> ReviewsCreated = Meter.CreateCounter<long>("community_reviews_created");
    private static readonly Counter<long> ReportsCreated = Meter.CreateCounter<long>("community_reports_created");
    private static readonly Histogram<double> FeedLatency = Meter.CreateHistogram<double>("community_feed_latency", unit: "ms");

    public static void PostCreated() => PostsCreated.Add(1);
    public static void CommentCreated() => CommentsCreated.Add(1);
    public static void ReviewCreated() => ReviewsCreated.Add(1);
    public static void ReportCreated() => ReportsCreated.Add(1);
    public static void Feed(double ms) => FeedLatency.Record(ms);
}

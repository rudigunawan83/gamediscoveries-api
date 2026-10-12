using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameDiscoveries.Modules.Administration.Features.Monitoring;

namespace GameDiscoveries.UnitTests.Administration;

public class SeoMonitoringTests
{
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    [Theory]
    [InlineData("24h", "2026-10-11", "2026-10-11", "2026-10-10", "2026-10-10")]
    [InlineData("7d", "2026-10-05", "2026-10-11", "2026-09-28", "2026-10-04")]
    [InlineData("30d", "2026-09-12", "2026-10-11", "2026-08-13", "2026-09-11")]
    public void Date_range_ends_yesterday_in_pacific_time(
        string range, string start, string end, string previousStart, string previousEnd)
    {
        // 05:00 UTC on Oct 13 is still Oct 12 in Los Angeles.
        var now = new DateTimeOffset(2026, 10, 13, 5, 0, 0, TimeSpan.Zero);

        var (current, previous) = SeoDateRange.For(MonitoringWindow.Parse(range, now), now, Pacific);

        current.Should().Be(new SeoDateRange(DateOnly.Parse(start), DateOnly.Parse(end)));
        previous.Should().Be(new SeoDateRange(DateOnly.Parse(previousStart), DateOnly.Parse(previousEnd)));
    }

    [Fact]
    public void Rankings_bucket_queries_by_position_and_fill_missing_days()
    {
        var range = new SeoDateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3));
        SearchAnalyticsRow Row(string day, double position) => new([day, $"q{position}"], 1, 10, 0.1, position);

        var points = SeoMonitoringService.Rankings(range, [
            Row("2026-10-01", 1.4),
            Row("2026-10-01", 3),
            Row("2026-10-01", 7.2),
            Row("2026-10-01", 48),
            Row("2026-10-01", 72),
            Row("2026-10-03", 10),
        ]);

        points.Should().HaveCount(3);
        points[0].Should().Match<SeoRankingPoint>(p => p.Top3 == 2 && p.Top10 == 1 && p.Top50 == 1);
        points[1].Should().Match<SeoRankingPoint>(p => p.Top3 == 0 && p.Top10 == 0 && p.Top50 == 0);
        points[2].Top10.Should().Be(1);
        points[0].Bucket.Should().Be(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Issues_rank_sitemap_errors_before_ctr_and_ranking_opportunities()
    {
        var pages = new[]
        {
            new SearchAnalyticsRow(["https://www.gamediscoveries.com/a"], 1, 400, 0.0026, 6.1),
            new SearchAnalyticsRow(["https://www.gamediscoveries.com/b"], 30, 400, 0.075, 2.0),
        };
        var queries = new[]
        {
            new SearchAnalyticsRow(["space game"], 2, 90, 0.02, 7.3),
            new SearchAnalyticsRow(["puzzle"], 9, 90, 0.1, 1.5),
            new SearchAnalyticsRow(["rare"], 0, 5, 0, 8),
        };
        var sitemaps = new[]
        {
            new SearchConsoleSitemap("https://www.gamediscoveries.com/sitemap.xml", null, DateTimeOffset.UtcNow, false, true, 100, 2, 0),
        };

        var issues = SeoMonitoringService.BuildIssues(pages, queries, sitemaps);

        issues.Select(issue => issue.Kind).Should().Equal("sitemap_errors", "low_ctr", "striking_distance");
        issues[1].Target.Should().Be("https://www.gamediscoveries.com/a");
        issues[1].Detail.Should().Contain("CTR 0.3%");
        issues[2].Target.Should().Be("space game");
    }

    [Fact]
    public void Vitals_are_rated_with_google_thresholds()
    {
        var vitals = SeoMonitoringService.Vitals(new CruxRecord(null, null, [
            new CruxMetric("largest_contentful_paint", 1800, 0.81234),
            new CruxMetric("interaction_to_next_paint", 320, 0.6),
            new CruxMetric("cumulative_layout_shift", 0.31, 0.4),
        ]));

        vitals.Metrics.Select(metric => (metric.Metric, metric.Rating)).Should().Equal(
            ("lcp", "good"), ("inp", "needs_improvement"), ("cls", "poor"));
        vitals.Metrics[0].GoodPercent.Should().Be(81.2);
    }

    [Fact]
    public void Crux_response_reads_string_percentiles_and_collection_period()
    {
        using var document = JsonDocument.Parse("""
            {
              "record": {
                "metrics": {
                  "largest_contentful_paint": { "histogram": [{ "start": 0, "end": 2500, "density": 0.7 }], "percentiles": { "p75": 2100 } },
                  "cumulative_layout_shift": { "histogram": [{ "start": "0.00", "end": "0.10", "density": 0.9 }], "percentiles": { "p75": "0.05" } }
                },
                "collectionPeriod": {
                  "firstDate": { "year": 2026, "month": 9, "day": 13 },
                  "lastDate": { "year": 2026, "month": 10, "day": 10 }
                }
              }
            }
            """);

        var record = SearchConsoleClient.ParseCrux(document.RootElement);

        record.Metrics.Should().HaveCount(2);
        record.Metrics[1].Should().Be(new CruxMetric("cumulative_layout_shift", 0.05, 0.9));
        record.LastDate.Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void Assertion_is_a_verifiable_rs256_jwt_for_the_read_only_scope()
    {
        using var rsa = RSA.Create(2048);
        var now = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

        var jwt = SearchConsoleClient.CreateAssertion(
            "seo@project.iam.gserviceaccount.com", rsa.ExportPkcs8PrivateKeyPem(), "https://oauth2.googleapis.com/token", now);

        var parts = jwt.Split('.');
        parts.Should().HaveCount(3);
        rsa.VerifyData(
                Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
                Base64Url.DecodeFromChars(parts[2]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1)
            .Should().BeTrue();

        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        claims.RootElement.GetProperty("scope").GetString().Should().Be("https://www.googleapis.com/auth/webmasters.readonly");
        claims.RootElement.GetProperty("exp").GetInt64().Should().Be(now.AddHours(1).ToUnixTimeSeconds());
    }
}

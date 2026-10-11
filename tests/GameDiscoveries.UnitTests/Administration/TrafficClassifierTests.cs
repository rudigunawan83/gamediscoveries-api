using FluentAssertions;
using GameDiscoveries.Modules.Administration.Features.Monitoring;

namespace GameDiscoveries.UnitTests.Administration;

public class TrafficClassifierTests
{
    [Theory]
    [InlineData(null, "gamediscoveries.com", null, false, TrafficClassifier.Direct)]
    [InlineData("", "gamediscoveries.com", null, false, TrafficClassifier.Direct)]
    [InlineData(null, "gamediscoveries.com", "organic", false, TrafficClassifier.OrganicSearch)]
    [InlineData("www.google.co.id", "gamediscoveries.com", null, false, TrafficClassifier.OrganicSearch)]
    [InlineData("duckduckgo.com", "gamediscoveries.com", null, false, TrafficClassifier.OrganicSearch)]
    [InlineData("t.co", "gamediscoveries.com", null, false, TrafficClassifier.Social)]
    [InlineData("m.facebook.com", "gamediscoveries.com", null, false, TrafficClassifier.Social)]
    [InlineData("www.reddit.com", "gamediscoveries.com", null, false, TrafficClassifier.Social)]
    [InlineData("blog.example.org", "gamediscoveries.com", null, false, TrafficClassifier.Referral)]
    [InlineData("www.google.com", "gamediscoveries.com", null, true, TrafficClassifier.PaidAds)]
    [InlineData(null, "gamediscoveries.com", "CPC", false, TrafficClassifier.PaidAds)]
    [InlineData("www.gamediscoveries.com", "gamediscoveries.com", null, false, TrafficClassifier.Others)]
    public void Source_classifies_landing_referrers(
        string? referrerHost,
        string? pageHost,
        string? utmMedium,
        bool hasAdClickId,
        string expected)
    {
        TrafficClassifier.Source(referrerHost, pageHost, utmMedium, hasAdClickId).Should().Be(expected);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36", "Chrome")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 Edg/130.0", "Edge")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1", "Chrome")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; rv:131.0) Gecko/20100101 Firefox/131.0", "Firefox")]
    [InlineData("Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 Chrome/122.0 Mobile Safari/537.36", "Samsung Internet")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 OPR/115.0", "Opera")]
    [InlineData("Dart/3.5 (dart:io)", "Other")]
    [InlineData(null, "Unknown")]
    public void Browser_reads_the_user_agent(string? userAgent, string expected)
    {
        TrafficClassifier.Browser(userAgent).Should().Be(expected);
    }

    [Theory]
    [InlineData("DESKTOP", "desktop")]
    [InlineData("mobile", "mobile")]
    [InlineData("TABLET", "tablet")]
    [InlineData(null, "unknown")]
    public void Device_normalizes_device_type(string? deviceType, string expected)
    {
        TrafficClassifier.Device(deviceType).Should().Be(expected);
    }

    [Theory]
    [InlineData("WEB", "WEB", "web")]
    [InlineData("WEB", "ANDROID", "web")]
    [InlineData(null, null, "web")]
    [InlineData("MOBILE", "ANDROID", "android")]
    [InlineData("mobile", "ios", "ios")]
    [InlineData("MOBILE", "UNKNOWN", "app")]
    public void Platform_separates_the_website_from_the_mobile_app(
        string? eventSource,
        string? platform,
        string expected)
    {
        TrafficClassifier.Platform(eventSource, platform).Should().Be(expected);
    }

    [Fact]
    public void Mobile_app_is_a_traffic_source_before_others()
    {
        TrafficClassifier.SourceOrder.Should().ContainInOrder(TrafficClassifier.MobileApp, TrafficClassifier.Others);
        TrafficClassifier.IsApp("MOBILE").Should().BeTrue();
        TrafficClassifier.IsApp("WEB").Should().BeFalse();
    }
}

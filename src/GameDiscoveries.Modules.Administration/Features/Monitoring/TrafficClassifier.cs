using System.Text.RegularExpressions;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public static partial class TrafficClassifier
{
    public const string OrganicSearch = "organic_search";
    public const string Direct = "direct";
    public const string Social = "social";
    public const string Referral = "referral";
    public const string PaidAds = "paid_ads";
    public const string Others = "others";

    public static readonly IReadOnlyList<string> SourceOrder =
        [OrganicSearch, Direct, Social, Referral, PaidAds, Others];

    private static readonly string[] PaidMediums =
        ["cpc", "ppc", "paid", "paidsearch", "paid_search", "paidsocial", "paid_social", "display", "cpm", "banner"];

    private static readonly string[] SearchEngines =
        ["google", "bing", "duckduckgo", "yahoo", "yandex", "baidu", "ecosia", "naver", "seznam", "brave"];

    private static readonly string[] SocialHosts =
    [
        "facebook.com", "fb.com", "fb.me", "instagram.com", "twitter.com", "x.com", "t.co",
        "tiktok.com", "youtube.com", "youtu.be", "reddit.com", "linkedin.com", "lnkd.in",
        "pinterest.com", "whatsapp.com", "wa.me", "t.me", "telegram.org", "discord.com",
        "discord.gg", "threads.net", "snapchat.com", "line.me"
    ];

    /// <summary>
    /// Classifies a visit by its landing referrer and campaign tags. Same-site
    /// referrers mean the analytics session started mid-visit, so they are
    /// grouped as others rather than counted as direct.
    /// </summary>
    public static string Source(string? referrerHost, string? pageHost, string? utmMedium, bool hasAdClickId)
    {
        var medium = utmMedium?.Trim().ToLowerInvariant();
        if (hasAdClickId || (medium is not null && PaidMediums.Contains(medium)))
        {
            return PaidAds;
        }

        var host = NormalizeHost(referrerHost);
        if (host is null)
        {
            return medium == "organic" ? OrganicSearch : Direct;
        }

        if (host == NormalizeHost(pageHost))
        {
            return Others;
        }

        if (SearchEngines.Any(engine => HasLabel(host, engine)))
        {
            return OrganicSearch;
        }

        if (SocialHosts.Any(social => host == social || host.EndsWith("." + social, StringComparison.Ordinal)))
        {
            return Social;
        }

        return Referral;
    }

    public static string Browser(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";
        if (EdgePattern().IsMatch(userAgent)) return "Edge";
        if (OperaPattern().IsMatch(userAgent)) return "Opera";
        if (userAgent.Contains("SamsungBrowser/", StringComparison.Ordinal)) return "Samsung Internet";
        if (FirefoxPattern().IsMatch(userAgent)) return "Firefox";
        if (ChromePattern().IsMatch(userAgent)) return "Chrome";
        if (userAgent.Contains("Safari/", StringComparison.Ordinal)) return "Safari";
        return "Other";
    }

    public static string Device(string? deviceType) =>
        deviceType?.Trim().ToUpperInvariant() switch
        {
            "DESKTOP" => "desktop",
            "MOBILE" => "mobile",
            "TABLET" => "tablet",
            _ => "unknown"
        };

    private static string? NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var value = host.Trim().ToLowerInvariant();
        return value.StartsWith("www.", StringComparison.Ordinal) ? value[4..] : value;
    }

    private static bool HasLabel(string host, string label) =>
        host.Split('.').Contains(label);

    [GeneratedRegex(@"\bEdg(e|A|iOS)?/")]
    private static partial Regex EdgePattern();

    [GeneratedRegex(@"\b(OPR|Opera)/")]
    private static partial Regex OperaPattern();

    [GeneratedRegex(@"\b(Firefox|FxiOS)/")]
    private static partial Regex FirefoxPattern();

    [GeneratedRegex(@"\b(Chrome|CriOS)/")]
    private static partial Regex ChromePattern();
}

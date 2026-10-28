using System.Globalization;
using System.Net;
using GameDiscoveries.BuildingBlocks.Caching;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public interface ISeoMonitoringService
{
    Task<MonitoringSeoResponse> GetSeoAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default);
}

/// <summary>Inclusive Search Console date range.</summary>
public readonly record struct SeoDateRange(DateOnly Start, DateOnly End)
{
    public IEnumerable<DateOnly> Days()
    {
        for (var day = Start; day <= End; day = day.AddDays(1)) yield return day;
    }

    /// <summary>
    /// Search Console days are Pacific Time and today is mostly empty, so ranges end yesterday.
    /// 24h maps to that single day.
    /// </summary>
    public static (SeoDateRange Current, SeoDateRange Previous) For(
        MonitoringWindow window,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var yesterday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).AddDays(-1);
        SeoDateRange current;
        if (window.Key.StartsWith("custom:", StringComparison.Ordinal))
        {
            // Inclusive calendar days; MonitoringWindow.To is exclusive midnight.
            var start = DateOnly.FromDateTime(window.From.UtcDateTime);
            var end = DateOnly.FromDateTime(window.To.UtcDateTime.AddTicks(-1));
            if (end > yesterday) end = yesterday;
            if (start > end) start = end;
            current = new SeoDateRange(start, end);
        }
        else
        {
            var days = window.Key switch { "24h" => 1, "7d" => 7, _ => 30 };
            current = new SeoDateRange(yesterday.AddDays(1 - days), yesterday);
        }

        var length = current.End.DayNumber - current.Start.DayNumber + 1;
        var previous = new SeoDateRange(current.Start.AddDays(-length), current.Start.AddDays(-1));
        return (current, previous);
    }
}

public sealed class SeoMonitoringService(
    ISearchConsoleClient searchConsole,
    ICacheService cache,
    TimeProvider time,
    ILogger<SeoMonitoringService> logger) : ISeoMonitoringService
{
    private const int TopLimit = 5;
    private const int IssueLimit = 10;
    private const int LowCtrMinImpressions = 100;
    private const double LowCtrMax = 0.01;
    private const int StrikingMinImpressions = 20;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FailureCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    private static readonly MonitoringUnavailable IndexCoverage = new(
        "Google does not offer the Page indexing report through its API. " +
        "Open Search Console → Indexing → Pages for indexed and excluded pages.");

    public async Task<MonitoringSeoResponse> GetSeoAsync(
        MonitoringWindow window,
        bool bypassCache,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"monitoring:seo:{window.Key}";
        if (!bypassCache)
        {
            var cached = await cache.GetAsync<MonitoringSeoResponse>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var seo = await LoadAsync(window, cancellationToken);
        var ttl = seo.SearchUnavailable is null ? CacheTtl : FailureCacheTtl;
        await cache.SetAsync(cacheKey, seo, ttl, cancellationToken);
        return seo;
    }

    private async Task<MonitoringSeoResponse> LoadAsync(MonitoringWindow window, CancellationToken cancellationToken)
    {
        var (current, previous) = SeoDateRange.For(window, time.GetUtcNow(), Pacific);

        var searchTask = LoadSearchAsync(current, previous, cancellationToken);
        var sitemapsTask = LoadSitemapsAsync(cancellationToken);
        var vitalsTask = LoadVitalsAsync(cancellationToken);
        await Task.WhenAll(searchTask, sitemapsTask, vitalsTask);

        var (search, pages, queries, searchUnavailable) = searchTask.Result;
        var (sitemaps, sitemapsUnavailable) = sitemapsTask.Result;
        var (vitals, vitalsUnavailable) = vitalsTask.Result;

        return new MonitoringSeoResponse(
            window.Key,
            Bucket(current.Start),
            Bucket(current.End),
            time.GetUtcNow(),
            searchConsole.SiteUrl,
            search,
            searchUnavailable,
            sitemaps,
            sitemapsUnavailable,
            vitals,
            vitalsUnavailable,
            BuildIssues(pages, queries, sitemaps ?? []),
            IndexCoverage);
    }

    private async Task<(SeoSearchPerformance?, IReadOnlyList<SearchAnalyticsRow>, IReadOnlyList<SearchAnalyticsRow>, MonitoringUnavailable?)>
        LoadSearchAsync(SeoDateRange current, SeoDateRange previous, CancellationToken cancellationToken)
    {
        if (!searchConsole.IsConfigured) return (null, [], [], NotConnected());

        try
        {
            Task<IReadOnlyList<SearchAnalyticsRow>> Query(SeoDateRange range, string[] dimensions, int limit) =>
                searchConsole.QueryAsync(range.Start, range.End, dimensions, limit, cancellationToken);

            var totals = Query(current, [], 1);
            var previousTotals = Query(previous, [], 1);
            var trend = Query(current, ["date"], 1000);
            var pages = Query(current, ["page"], 250);
            var queries = Query(current, ["query"], 250);
            var countries = Query(current, ["country"], TopLimit);
            var devices = Query(current, ["device"], TopLimit);
            var rankings = Query(current, ["date", "query"], 25_000);
            await Task.WhenAll(totals, previousTotals, trend, pages, queries, countries, devices, rankings);

            var now = Totals(totals.Result);
            var before = Totals(previousTotals.Result);

            var search = new SeoSearchPerformance(
                new MonitoringCount(now.Clicks, MonitoringMath.ChangePercent(now.Clicks, before.Clicks)),
                new MonitoringCount(now.Impressions, MonitoringMath.ChangePercent(now.Impressions, before.Impressions)),
                new MonitoringRatio(now.Ctr, MonitoringMath.ChangePercent(now.Ctr, before.Ctr)),
                new MonitoringRatio(now.Position, MonitoringMath.ChangePercent(now.Position, before.Position)),
                Trend(current, trend.Result),
                Rankings(current, rankings.Result),
                pages.Result.Take(TopLimit).Select(row => Row(row, row.Keys[0])).ToList(),
                queries.Result.Take(TopLimit).Select(row => Row(row, row.Keys[0])).ToList(),
                countries.Result.Select(row => Row(row, CountryCodes.Normalize(row.Keys[0]))).ToList(),
                devices.Result.Select(row => Row(row, row.Keys[0].ToLowerInvariant())).ToList());

            return (search, pages.Result, queries.Result, null);
        }
        catch (Exception exception) when (IsGoogleFailure(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Search Console analytics could not be loaded.");
            return (null, [], [], Failure(exception));
        }
    }

    private async Task<(IReadOnlyList<SearchConsoleSitemap>?, MonitoringUnavailable?)> LoadSitemapsAsync(
        CancellationToken cancellationToken)
    {
        if (!searchConsole.IsConfigured) return (null, NotConnected());

        try
        {
            return (await searchConsole.ListSitemapsAsync(cancellationToken), null);
        }
        catch (Exception exception) when (IsGoogleFailure(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Search Console sitemaps could not be loaded.");
            return (null, Failure(exception));
        }
    }

    private async Task<(SeoCoreWebVitals?, MonitoringUnavailable?)> LoadVitalsAsync(CancellationToken cancellationToken)
    {
        if (!searchConsole.HasCruxKey)
        {
            return (null, new MonitoringUnavailable(
                "Core Web Vitals need a Chrome UX Report API key in SearchConsole:CruxApiKey."));
        }

        try
        {
            var record = await searchConsole.QueryCoreWebVitalsAsync(cancellationToken);
            if (record is null)
            {
                return (null, new MonitoringUnavailable(
                    "Chrome UX Report does not have enough real-user Chrome traffic for this site yet."));
            }

            return (Vitals(record), null);
        }
        catch (Exception exception) when (IsGoogleFailure(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Chrome UX Report could not be loaded.");
            return (null, new MonitoringUnavailable(exception is SearchConsoleException
                ? exception.Message
                : "Chrome UX Report could not be reached."));
        }
    }

    public static SeoCoreWebVitals Vitals(CruxRecord record) => new(
        record.FirstDate,
        record.LastDate,
        record.Metrics.Select(metric =>
        {
            var (key, good, poor, digits) = metric.Name switch
            {
                "largest_contentful_paint" => ("lcp", 2500d, 4000d, 0),
                "interaction_to_next_paint" => ("inp", 200d, 500d, 0),
                _ => ("cls", 0.1, 0.25, 2),
            };
            string? rating = metric.P75 switch
            {
                null => null,
                var value when value <= good => "good",
                var value when value <= poor => "needs_improvement",
                _ => "poor",
            };
            return new SeoVital(
                key,
                metric.P75 is { } p75 ? Math.Round(p75, digits) : null,
                metric.GoodDensity is { } density ? Math.Round(density * 100, 1) : null,
                rating);
        }).ToList());

    public static IReadOnlyList<SeoRankingPoint> Rankings(SeoDateRange range, IEnumerable<SearchAnalyticsRow> rows)
    {
        var byDay = rows
            .Where(row => row.Keys.Count >= 2)
            .GroupBy(row => row.Keys[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        return range.Days().Select(day =>
        {
            var dayRows = byDay.GetValueOrDefault(DayKey(day)) ?? [];
            return new SeoRankingPoint(
                Bucket(day),
                dayRows.Count(row => row.Position <= 3),
                dayRows.Count(row => row.Position > 3 && row.Position <= 10),
                dayRows.Count(row => row.Position > 10 && row.Position <= 50));
        }).ToList();
    }

    public static IReadOnlyList<SeoIssue> BuildIssues(
        IEnumerable<SearchAnalyticsRow> pages,
        IEnumerable<SearchAnalyticsRow> queries,
        IEnumerable<SearchConsoleSitemap> sitemaps)
    {
        var issues = new List<(int Rank, double Weight, SeoIssue Issue)>();

        foreach (var sitemap in sitemaps)
        {
            if (sitemap.Errors > 0)
            {
                issues.Add((0, sitemap.Errors, new SeoIssue(
                    "sitemap_errors", "high", sitemap.Path, $"{sitemap.Errors:N0} sitemap errors reported by Google.")));
            }

            if (sitemap.Warnings > 0)
            {
                issues.Add((1, sitemap.Warnings, new SeoIssue(
                    "sitemap_warnings", "medium", sitemap.Path, $"{sitemap.Warnings:N0} sitemap warnings reported by Google.")));
            }

            if (sitemap.LastDownloaded is null)
            {
                issues.Add((2, 0, new SeoIssue(
                    "sitemap_not_read", "low", sitemap.Path, "Google has not downloaded this sitemap yet.")));
            }
        }

        foreach (var page in pages.Where(row =>
                     row.Keys.Count > 0 && row.Impressions >= LowCtrMinImpressions && row.Ctr < LowCtrMax))
        {
            issues.Add((1, page.Impressions, new SeoIssue(
                "low_ctr",
                "medium",
                page.Keys[0],
                string.Create(CultureInfo.InvariantCulture,
                    $"CTR {page.Ctr * 100:0.0}% from {page.Impressions:N0} impressions at position {page.Position:0.0}. Improve the title and description."))));
        }

        foreach (var query in queries.Where(row =>
                     row.Keys.Count > 0 && row.Position > 3 && row.Position <= 20 && row.Impressions >= StrikingMinImpressions))
        {
            issues.Add((2, query.Impressions, new SeoIssue(
                "striking_distance",
                "low",
                query.Keys[0],
                string.Create(CultureInfo.InvariantCulture,
                    $"Position {query.Position:0.0} with {query.Impressions:N0} impressions. Close to the top 3."))));
        }

        return issues
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.Weight)
            .Take(IssueLimit)
            .Select(item => item.Issue)
            .ToList();
    }

    private static IReadOnlyList<SeoTrendPoint> Trend(SeoDateRange range, IEnumerable<SearchAnalyticsRow> rows)
    {
        var byDay = rows
            .Where(row => row.Keys.Count > 0)
            .ToDictionary(row => row.Keys[0], StringComparer.Ordinal);

        return range.Days().Select(day =>
        {
            if (!byDay.TryGetValue(DayKey(day), out var row)) return new SeoTrendPoint(Bucket(day), 0, 0, null, null);
            var metric = Row(row, DayKey(day));
            return new SeoTrendPoint(Bucket(day), metric.Clicks, metric.Impressions, metric.Ctr, metric.Position);
        }).ToList();
    }

    private static SeoMetricRow Totals(IReadOnlyList<SearchAnalyticsRow> rows) =>
        rows.Count == 0 ? new SeoMetricRow("", 0, 0, null, null) : Row(rows[0], "");

    private static SeoMetricRow Row(SearchAnalyticsRow row, string key)
    {
        var impressions = (long)Math.Round(row.Impressions);
        return new SeoMetricRow(
            key,
            (long)Math.Round(row.Clicks),
            impressions,
            impressions > 0 ? Math.Round(row.Ctr * 100, 2) : null,
            impressions > 0 ? Math.Round(row.Position, 1) : null);
    }

    /// <summary>Noon UTC keeps the calendar day the same in every browser time zone.</summary>
    private static DateTimeOffset Bucket(DateOnly day) => new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private static string DayKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private MonitoringUnavailable NotConnected() => new(
        $"Search Console is not connected. Configure a service account key (SearchConsole:ServiceAccountKeyPath) " +
        $"and add the service account as a user of {searchConsole.SiteUrl}.");

    private MonitoringUnavailable Failure(Exception exception)
    {
        if (exception is SearchConsoleException { Status: HttpStatusCode.Forbidden } forbidden
            && forbidden.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return new MonitoringUnavailable(
                $"The service account {searchConsole.ServiceAccountEmail} has no access to {searchConsole.SiteUrl}. " +
                "Add it in Search Console → Settings → Users and permissions.");
        }

        return new MonitoringUnavailable(exception is SearchConsoleException
            ? exception.Message
            : "Search Console could not be reached.");
    }

    private static bool IsGoogleFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is SearchConsoleException or HttpRequestException or System.Text.Json.JsonException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}

internal static class CountryCodes
{
    private static readonly Lazy<Dictionary<string, string>> Alpha2ByAlpha3 = new(Build);

    /// <summary>Search Console reports ISO alpha-3 (`idn`); returns alpha-2 (`ID`) when known.</summary>
    public static string Normalize(string alpha3)
    {
        var key = alpha3.ToUpperInvariant();
        return Alpha2ByAlpha3.Value.TryGetValue(key, out var alpha2) ? alpha2 : key;
    }

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                map.TryAdd(region.ThreeLetterISORegionName, region.TwoLetterISORegionName);
            }
            catch (ArgumentException)
            {
            }
        }

        return map;
    }
}

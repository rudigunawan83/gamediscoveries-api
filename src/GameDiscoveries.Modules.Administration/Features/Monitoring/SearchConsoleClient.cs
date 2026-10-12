using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Administration.Features.Monitoring;

public sealed record SearchAnalyticsRow(
    IReadOnlyList<string> Keys,
    double Clicks,
    double Impressions,
    double Ctr,
    double Position);

public sealed record SearchConsoleSitemap(
    string Path,
    DateTimeOffset? LastSubmitted,
    DateTimeOffset? LastDownloaded,
    bool IsPending,
    bool IsSitemapsIndex,
    long Submitted,
    long Errors,
    long Warnings);

/// <summary>A CrUX metric: p75 in milliseconds (CLS is unitless) and the share of good experiences.</summary>
public sealed record CruxMetric(string Name, double? P75, double? GoodDensity);

public sealed record CruxRecord(DateOnly? FirstDate, DateOnly? LastDate, IReadOnlyList<CruxMetric> Metrics);

public sealed class SearchConsoleException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

public interface ISearchConsoleClient
{
    string SiteUrl { get; }

    bool IsConfigured { get; }

    bool HasCruxKey { get; }

    string? ServiceAccountEmail { get; }

    /// <summary>Search Analytics for web search, including fresh (not yet final) data.</summary>
    Task<IReadOnlyList<SearchAnalyticsRow>> QueryAsync(
        DateOnly start,
        DateOnly end,
        IReadOnlyList<string> dimensions,
        int rowLimit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchConsoleSitemap>> ListSitemapsAsync(CancellationToken cancellationToken = default);

    /// <summary>Origin-level Core Web Vitals; null when Chrome has too little traffic for the origin.</summary>
    Task<CruxRecord?> QueryCoreWebVitalsAsync(CancellationToken cancellationToken = default);
}

public sealed class SearchConsoleClient : ISearchConsoleClient
{
    public const string HttpClientName = "google-search-console";

    public static readonly string[] CruxMetrics =
        ["largest_contentful_paint", "interaction_to_next_paint", "cumulative_layout_shift"];

    private const string Scope = "https://www.googleapis.com/auth/webmasters.readonly";
    private const string ApiBase = "https://www.googleapis.com/webmasters/v3/sites/";
    private const string CruxUrl = "https://chromeuxreport.googleapis.com/v1/records:queryRecord";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly SearchConsoleOptions options;
    private readonly TimeProvider time;
    private readonly ILogger<SearchConsoleClient> logger;
    private readonly Lazy<ServiceAccountKey?> key;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private CachedToken? token;

    public SearchConsoleClient(
        IHttpClientFactory httpClientFactory,
        IOptions<SearchConsoleOptions> options,
        TimeProvider time,
        ILogger<SearchConsoleClient> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.time = time;
        this.logger = logger;
        key = new Lazy<ServiceAccountKey?>(LoadKey);
    }

    public string SiteUrl => options.SiteUrl;

    public bool IsConfigured => key.Value is not null;

    public bool HasCruxKey => !string.IsNullOrWhiteSpace(options.CruxApiKey);

    public string? ServiceAccountEmail => key.Value?.ClientEmail;

    public async Task<IReadOnlyList<SearchAnalyticsRow>> QueryAsync(
        DateOnly start,
        DateOnly end,
        IReadOnlyList<string> dimensions,
        int rowLimit,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            startDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            endDate = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            dimensions,
            type = "web",
            dataState = "all",
            rowLimit,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, SiteEndpoint("searchAnalytics/query"))
        {
            Content = JsonContent.Create(body, options: Json),
        };
        var payload = await SendAsync<SearchAnalyticsResponse>(request, cancellationToken);
        return payload.Rows?
            .Select(row => new SearchAnalyticsRow(row.Keys ?? [], row.Clicks, row.Impressions, row.Ctr, row.Position))
            .ToList() ?? [];
    }

    public async Task<IReadOnlyList<SearchConsoleSitemap>> ListSitemapsAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SiteEndpoint("sitemaps"));
        var payload = await SendAsync<SitemapsResponse>(request, cancellationToken);
        return payload.Sitemap?
            .Select(sitemap =>
            {
                var contents = sitemap.Contents ?? [];
                var web = contents.Where(content => content.Type == "web").ToList();
                return new SearchConsoleSitemap(
                    sitemap.Path ?? "",
                    sitemap.LastSubmitted,
                    sitemap.LastDownloaded,
                    sitemap.IsPending,
                    sitemap.IsSitemapsIndex,
                    (web.Count > 0 ? web : contents).Sum(content => content.Submitted),
                    sitemap.Errors,
                    sitemap.Warnings);
            })
            .ToList() ?? [];
    }

    public async Task<CruxRecord?> QueryCoreWebVitalsAsync(CancellationToken cancellationToken = default)
    {
        if (!HasCruxKey) throw new SearchConsoleException("Chrome UX Report is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, CruxUrl)
        {
            Content = JsonContent.Create(new { origin = options.CruxOrigin, metrics = CruxMetrics }, options: Json),
        };
        // Header, not ?key=, so the key never shows up in HttpClient request logs.
        request.Headers.Add("X-Goog-Api-Key", options.CruxApiKey);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, "Chrome UX Report", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseCrux(document.RootElement);
    }

    public static CruxRecord ParseCrux(JsonElement root)
    {
        var record = root.GetProperty("record");
        var metrics = new List<CruxMetric>();
        if (record.TryGetProperty("metrics", out var all))
        {
            foreach (var name in CruxMetrics)
            {
                if (!all.TryGetProperty(name, out var metric)) continue;
                double? p75 = null;
                if (metric.TryGetProperty("percentiles", out var percentiles)
                    && percentiles.TryGetProperty("p75", out var value))
                {
                    p75 = ReadNumber(value);
                }

                double? good = null;
                if (metric.TryGetProperty("histogram", out var histogram)
                    && histogram.ValueKind == JsonValueKind.Array
                    && histogram.GetArrayLength() > 0
                    && histogram[0].TryGetProperty("density", out var density))
                {
                    good = ReadNumber(density);
                }

                metrics.Add(new CruxMetric(name, p75, good));
            }
        }

        DateOnly? first = null, last = null;
        if (record.TryGetProperty("collectionPeriod", out var period))
        {
            first = ReadDate(period, "firstDate");
            last = ReadDate(period, "lastDate");
        }

        return new CruxRecord(first, last, metrics);
    }

    /// <summary>Signed JWT for the OAuth service-account (jwt-bearer) grant.</summary>
    public static string CreateAssertion(string clientEmail, string privateKeyPem, string audience, DateTimeOffset now)
    {
        var header = Base64Url.EncodeToString("""{"alg":"RS256","typ":"JWT"}"""u8);
        var claims = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = clientEmail,
            scope = Scope,
            aud = audience,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds(),
        });
        var unsigned = $"{header}.{Base64Url.EncodeToString(claims)}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url.EncodeToString(signature)}";
    }

    private string SiteEndpoint(string path) => $"{ApiBase}{Uri.EscapeDataString(options.SiteUrl)}/{path}";

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "Search Console", cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new SearchConsoleException("Search Console returned an empty response.");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var account = key.Value ?? throw new SearchConsoleException("Search Console is not configured.");
        var cached = token;
        if (cached is not null && time.GetUtcNow() < cached.ExpiresAt) return cached.Value;

        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            var now = time.GetUtcNow();
            cached = token;
            if (cached is not null && now < cached.ExpiresAt) return cached.Value;

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = CreateAssertion(account.ClientEmail, account.PrivateKey, account.TokenUri, now),
            });
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsync(account.TokenUri, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new SearchConsoleException(
                    $"Google rejected the service account key ({(int)response.StatusCode}).",
                    response.StatusCode);
            }

            var issued = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken);
            if (string.IsNullOrEmpty(issued?.AccessToken))
            {
                throw new SearchConsoleException("Google returned no access token.");
            }

            token = new CachedToken(issued.AccessToken, now.AddSeconds(Math.Max(120, issued.ExpiresIn) - 60));
            return issued.AccessToken;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string service, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        string? detail = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<GoogleErrorResponse>(Json, cancellationToken);
            detail = error?.Error?.Message;
        }
        catch (JsonException)
        {
        }

        var message = $"{service} request failed ({(int)response.StatusCode})";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            message += $": {(detail.Length > 200 ? detail[..200] : detail)}";
        }

        throw new SearchConsoleException(message, response.StatusCode);
    }

    private ServiceAccountKey? LoadKey()
    {
        try
        {
            string? json = null;
            if (!string.IsNullOrWhiteSpace(options.ServiceAccountKeyPath) && File.Exists(options.ServiceAccountKeyPath))
            {
                json = File.ReadAllText(options.ServiceAccountKeyPath);
            }
            else if (!string.IsNullOrWhiteSpace(options.ServiceAccountJsonBase64))
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(options.ServiceAccountJsonBase64.Trim()));
            }

            if (json is null) return null;

            var parsed = JsonSerializer.Deserialize<ServiceAccountFile>(json);
            if (string.IsNullOrWhiteSpace(parsed?.ClientEmail) || string.IsNullOrWhiteSpace(parsed.PrivateKey))
            {
                logger.LogWarning("Search Console service account key has no client_email or private_key.");
                return null;
            }

            return new ServiceAccountKey(
                parsed.ClientEmail,
                parsed.PrivateKey,
                string.IsNullOrWhiteSpace(parsed.TokenUri) ? "https://oauth2.googleapis.com/token" : parsed.TokenUri);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            logger.LogWarning(exception, "Search Console service account key could not be read.");
            return null;
        }
    }

    private static double? ReadNumber(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    private static DateOnly? ReadDate(JsonElement period, string name)
    {
        if (!period.TryGetProperty(name, out var date)) return null;
        try
        {
            return new DateOnly(
                date.GetProperty("year").GetInt32(),
                date.GetProperty("month").GetInt32(),
                date.GetProperty("day").GetInt32());
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt);

    private sealed record ServiceAccountKey(string ClientEmail, string PrivateKey, string TokenUri);

    private sealed class ServiceAccountFile
    {
        [JsonPropertyName("client_email")]
        public string? ClientEmail { get; init; }

        [JsonPropertyName("private_key")]
        public string? PrivateKey { get; init; }

        [JsonPropertyName("token_uri")]
        public string? TokenUri { get; init; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed class SearchAnalyticsResponse
    {
        public List<SearchAnalyticsApiRow>? Rows { get; init; }
    }

    private sealed class SearchAnalyticsApiRow
    {
        public List<string>? Keys { get; init; }
        public double Clicks { get; init; }
        public double Impressions { get; init; }
        public double Ctr { get; init; }
        public double Position { get; init; }
    }

    private sealed class SitemapsResponse
    {
        public List<SitemapApi>? Sitemap { get; init; }
    }

    private sealed class SitemapApi
    {
        public string? Path { get; init; }
        public DateTimeOffset? LastSubmitted { get; init; }
        public DateTimeOffset? LastDownloaded { get; init; }
        public bool IsPending { get; init; }
        public bool IsSitemapsIndex { get; init; }
        public long Errors { get; init; }
        public long Warnings { get; init; }
        public List<SitemapContentApi>? Contents { get; init; }
    }

    private sealed class SitemapContentApi
    {
        public string? Type { get; init; }
        public long Submitted { get; init; }
    }

    private sealed class GoogleErrorResponse
    {
        public GoogleError? Error { get; init; }
    }

    private sealed class GoogleError
    {
        public string? Message { get; init; }
    }
}

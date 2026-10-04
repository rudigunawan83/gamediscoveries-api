using System.Net.Http.Json;
using System.Text.Json;
using GameDiscoveries.BuildingBlocks.Feeds;
using GameDiscoveries.Infrastructure.Providers.GameMonetize.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public sealed class GameMonetizeClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptions<GameMonetizeOptions> _options;
    private readonly ILogger<GameMonetizeClient> _logger;

    public GameMonetizeClient(
        HttpClient httpClient,
        IOptions<GameMonetizeOptions> options,
        ILogger<GameMonetizeClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        var timeoutSeconds = Math.Clamp(options.Value.TimeoutSeconds, 5, 120);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public Task<IReadOnlyList<GameMonetizeFeedItem>> FetchFeedAsync(
        CancellationToken cancellationToken = default)
        => FetchFeedAsync(GameFeedType.Latest, cancellationToken);

    public async Task<IReadOnlyList<GameMonetizeFeedItem>> FetchFeedAsync(
        GameFeedType feedType,
        CancellationToken cancellationToken = default)
    {
        var feedUrl = _options.Value.ResolveFeedUrl(feedType);
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            _logger.LogWarning("GameMonetize feed URL is not configured for {FeedType}", feedType);
            return [];
        }

        var attempts = Math.Clamp(_options.Value.MaxRetryAttempts, 1, 5);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
                if (!string.IsNullOrWhiteSpace(_options.Value.ApiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", _options.Value.ApiKey);
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if ((int)response.StatusCode is >= 500 or 408 or 429)
                {
                    response.EnsureSuccessStatusCode();
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException(
                        $"GameMonetize feed {feedType} returned {(int)response.StatusCode}: {body}");
                }

                var items = await response.Content.ReadFromJsonAsync<List<GameMonetizeFeedItem>>(
                    SerializerOptions,
                    cancellationToken);

                return items ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                lastError = ex;
                _logger.LogWarning(
                    ex,
                    "GameMonetize feed {FeedType} attempt {Attempt}/{Attempts} failed",
                    feedType,
                    attempt,
                    attempts);

                if (attempt == attempts)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
        }

        _logger.LogError(lastError, "Failed to fetch GameMonetize feed {FeedType} from {FeedUrl}", feedType, feedUrl);
        throw lastError ?? new InvalidOperationException($"Failed to fetch GameMonetize feed {feedType}");
    }
}

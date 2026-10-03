using System.Net.Http.Json;
using System.Text.Json;
using GameDiscoveries.Infrastructure.Providers.GameMonetize.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public sealed class GameMonetizeClient(
    HttpClient httpClient,
    IOptions<GameMonetizeOptions> options,
    ILogger<GameMonetizeClient> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<GameMonetizeFeedItem>> FetchFeedAsync(
        CancellationToken cancellationToken = default)
    {
        var feedUrl = options.Value.FeedUrl;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            logger.LogWarning("GameMonetize FeedUrl is not configured");
            return [];
        }

        try
        {
            var response = await httpClient.GetAsync(feedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<GameMonetizeFeedItem>>(
                SerializerOptions,
                cancellationToken);

            return items ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch GameMonetize feed from {FeedUrl}", feedUrl);
            throw;
        }
    }
}

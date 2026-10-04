using GameDiscoveries.BuildingBlocks.Caching;
using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Domain;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Recommendation.Services;

public interface IRecommendationCache
{
    string BuildKey(RecommendationType type, Guid? userId, Guid? gameId, int limit);
    TimeSpan GetTtl(RecommendationType type);
    Task<RecommendationResponse?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, RecommendationResponse value, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public sealed class RecommendationCache(
    ICacheService cache,
    IOptions<RecommendationOptions> options) : IRecommendationCache
{
    public string BuildKey(RecommendationType type, Guid? userId, Guid? gameId, int limit)
    {
        var subject = userId?.ToString("N") ?? "anonymous";
        var gamePart = gameId?.ToString("N") ?? "-";
        return $"recommendations:{type.ToApiValue()}:{subject}:{gamePart}:{limit}";
    }

    public TimeSpan GetTtl(RecommendationType type)
    {
        var ttl = options.Value.CacheTtl;
        var seconds = type switch
        {
            RecommendationType.ForYou => ttl.ForYouSeconds,
            RecommendationType.BecauseYouPlayed => ttl.BecauseYouPlayedSeconds,
            RecommendationType.SimilarGames => ttl.SimilarSeconds,
            RecommendationType.Trending => ttl.TrendingSeconds,
            RecommendationType.NewDiscoveries => ttl.NewDiscoveriesSeconds,
            RecommendationType.HiddenGems => ttl.HiddenGemsSeconds,
            _ => ttl.ForYouSeconds
        };
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 30, 3600));
    }

    public Task<RecommendationResponse?> GetAsync(string key, CancellationToken cancellationToken = default)
        => cache.GetAsync<RecommendationResponse>(key, cancellationToken);

    public Task SetAsync(string key, RecommendationResponse value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => cache.SetAsync(key, value, ttl, cancellationToken);
}

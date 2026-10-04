using System.Diagnostics;
using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Data;
using GameDiscoveries.Modules.Recommendation.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Recommendation.Services;

public interface IRecommendationEngine
{
    Task<RecommendationResponse> GetAsync(
        RecommendationType type,
        Guid? userId,
        Guid? seedGameId,
        int? limit,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    Task<RecommendationDebugResponse> GetDebugAsync(
        RecommendationType type,
        Guid? userId,
        Guid? seedGameId,
        int? limit,
        CancellationToken cancellationToken = default);
}

public sealed class RecommendationEngine(
    IRecommendationRepository repository,
    IRecommendationCache cache,
    IOptions<RecommendationOptions> optionsAccessor,
    ILogger<RecommendationEngine> logger) : IRecommendationEngine
{
    public async Task<RecommendationResponse> GetAsync(
        RecommendationType type,
        Guid? userId,
        Guid? seedGameId,
        int? limit,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        var take = Math.Clamp(limit ?? options.DefaultLimit, 1, 50);
        var key = cache.BuildKey(type, userId, seedGameId, take);
        var sw = Stopwatch.StartNew();

        if (!bypassCache)
        {
            var cached = await cache.GetAsync(key, cancellationToken);
            if (cached is not null)
            {
                RecommendationMetrics.RecordRequest(type.ToApiValue(), true, cached.Items.Count, cached.Items.Count, sw.Elapsed.TotalMilliseconds);
                logger.LogInformation(
                    "Recommendation cache hit type={Type} user={UserId} count={Count} version={Version}",
                    type.ToApiValue(), userId, cached.Items.Count, options.AlgorithmVersion);
                return cached with { CacheHit = true };
            }
        }

        var (response, candidateCount, coldStart) = await GenerateAsync(type, userId, seedGameId, take, cancellationToken);
        var ttl = cache.GetTtl(type);
        var payload = response with
        {
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            CacheHit = false
        };

        await cache.SetAsync(key, payload with { CacheHit = false }, ttl, cancellationToken);

        RecommendationMetrics.RecordRequest(type.ToApiValue(), false, candidateCount, payload.Items.Count, sw.Elapsed.TotalMilliseconds);
        logger.LogInformation(
            "Recommendation generated type={Type} user={UserId} candidates={Candidates} results={Results} coldStart={ColdStart} durationMs={Duration} version={Version}",
            type.ToApiValue(), userId, candidateCount, payload.Items.Count, coldStart, sw.Elapsed.TotalMilliseconds, options.AlgorithmVersion);

        return payload;
    }

    public async Task<RecommendationDebugResponse> GetDebugAsync(
        RecommendationType type,
        Guid? userId,
        Guid? seedGameId,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.Value;
        var take = Math.Clamp(limit ?? options.DefaultLimit, 1, 50);
        var profile = await repository.BuildProfileAsync(userId, cancellationToken);
        var seed = seedGameId is null ? null : await repository.GetGameAsync(seedGameId.Value, cancellationToken);
        var candidates = await CollectCandidatesAsync(type, profile, seed, options, cancellationToken);
        var scored = Rank(type, candidates, profile, seed, options);

        return new RecommendationDebugResponse(
            type.ToApiValue(),
            options.AlgorithmVersion,
            candidates.Count,
            false,
            profile.IsColdStart,
            scored.Take(take).Select((item, index) => new RecommendationDebugItem(
                item.Game.Id,
                item.Game.Title,
                item.Game.Category,
                item.Score,
                index + 1,
                item.Score.Reason)).ToList());
    }

    private async Task<(RecommendationResponse Response, int CandidateCount, bool ColdStart)> GenerateAsync(
        RecommendationType type,
        Guid? userId,
        Guid? seedGameId,
        int take,
        CancellationToken cancellationToken)
    {
        var options = optionsAccessor.Value;
        var now = DateTimeOffset.UtcNow;
        var profile = await repository.BuildProfileAsync(userId, cancellationToken);
        var seed = seedGameId is null ? null : await repository.GetGameAsync(seedGameId.Value, cancellationToken);
        var candidates = await CollectCandidatesAsync(type, profile, seed, options, cancellationToken);
        var ranked = Rank(type, candidates, profile, seed, options);
        var diversified = DiversityService.Diversify(
            ranked,
            take,
            options.MaxSameCategoryInTop10,
            options.Weights.Diversity).ToList();

        InjectExploration(diversified, ranked, take, options);

        var items = diversified
            .Take(take)
            .Select((item, index) => new RecommendationItemDto(
                new RecommendationGameDto(
                    item.Game.Id,
                    item.Game.Slug,
                    item.Game.Title,
                    item.Game.ThumbnailUrl,
                    item.Game.Category,
                    item.Game.Orientation),
                Math.Round(item.Score.Final, 4),
                index + 1,
                item.Score.Reason))
            .ToList();

        // Never return empty when catalog has games — cold-start fallback.
        if (items.Count == 0)
        {
            var fallback = await repository.GetTrendingAsync(take, cancellationToken: cancellationToken);
            items = fallback.Select((game, index) => new RecommendationItemDto(
                new RecommendationGameDto(game.Id, game.Slug, game.Title, game.ThumbnailUrl, game.Category, game.Orientation),
                Math.Round(0.5 - (index * 0.01), 4),
                index + 1,
                "Trending now")).ToList();
        }

        var response = new RecommendationResponse(
            items,
            type.ToApiValue(),
            options.AlgorithmVersion,
            now,
            now,
            false);

        return (response, candidates.Count, profile.IsColdStart);
    }

    private async Task<List<CandidateGame>> CollectCandidatesAsync(
        RecommendationType type,
        UserPreferenceProfile profile,
        CandidateGame? seed,
        RecommendationOptions options,
        CancellationToken cancellationToken)
    {
        var exclude = BuildExclusions(type, profile, options);
        var limit = options.CandidateLimit;
        var map = new Dictionary<Guid, CandidateGame>();

        async Task AddRange(IEnumerable<CandidateGame> games)
        {
            foreach (var game in games)
            {
                map.TryAdd(game.Id, game);
            }

            await Task.CompletedTask;
        }

        switch (type)
        {
            case RecommendationType.SimilarGames:
                if (seed is null) break;
                await AddRange(await repository.GetSimilarCandidatesAsync(seed, limit, exclude, cancellationToken));
                break;

            case RecommendationType.BecauseYouPlayed:
                foreach (var seedId in profile.RecentSeedGameIds.Take(5))
                {
                    var played = await repository.GetGameAsync(seedId, cancellationToken);
                    if (played is null) continue;
                    await AddRange(await repository.GetSimilarCandidatesAsync(played, Math.Max(20, limit / 5), exclude, cancellationToken));
                }
                break;

            case RecommendationType.Trending:
                await AddRange(await repository.GetTrendingAsync(limit, exclude, cancellationToken));
                break;

            case RecommendationType.NewDiscoveries:
                await AddRange(await repository.GetNewestAsync(limit, exclude, cancellationToken));
                break;

            case RecommendationType.HiddenGems:
                await AddRange(await repository.GetHiddenGemCandidatesAsync(limit, exclude, cancellationToken));
                if (map.Count < takeSafe(limit / 2))
                {
                    await AddRange(await repository.GetCandidatesAsync(limit, exclude, cancellationToken));
                }
                break;

            case RecommendationType.QuickPlay:
                await AddRange(await repository.GetByFeedAsync("Mobile", limit / 2, exclude, cancellationToken));
                await AddRange(await repository.GetNewestAsync(limit / 2, exclude, cancellationToken));
                break;

            case RecommendationType.ForYou:
            default:
                if (profile.IsColdStart)
                {
                    var trendingN = (int)(limit * options.ColdStart.Trending);
                    var popularN = (int)(limit * options.ColdStart.Popular);
                    var newN = (int)(limit * options.ColdStart.NewDiscoveries);
                    var exploreN = Math.Max(1, limit - trendingN - popularN - newN);
                    await AddRange(await repository.GetTrendingAsync(trendingN, exclude, cancellationToken));
                    await AddRange(await repository.GetByFeedAsync("Popular", popularN, exclude, cancellationToken));
                    await AddRange(await repository.GetNewestAsync(newN, exclude, cancellationToken));
                    await AddRange(await repository.GetHiddenGemCandidatesAsync(exploreN, exclude, cancellationToken));
                }
                else
                {
                    await AddRange(await repository.GetCandidatesAsync(limit, exclude, cancellationToken));
                    await AddRange(await repository.GetByFeedAsync("Popular", limit / 4, exclude, cancellationToken));
                    await AddRange(await repository.GetNewestAsync(limit / 5, exclude, cancellationToken));
                    await AddRange(await repository.GetTrendingAsync(limit / 5, exclude, cancellationToken));
                    foreach (var seedId in profile.RecentSeedGameIds.Take(3))
                    {
                        var played = await repository.GetGameAsync(seedId, cancellationToken);
                        if (played is null) continue;
                        await AddRange(await repository.GetSimilarCandidatesAsync(played, 30, exclude, cancellationToken));
                    }
                }
                break;
        }

        if (map.Count == 0)
        {
            await AddRange(await repository.GetTrendingAsync(limit, exclude, cancellationToken));
            await AddRange(await repository.GetNewestAsync(limit, exclude, cancellationToken));
        }

        return map.Values.ToList();
    }

    private List<ScoredCandidate> Rank(
        RecommendationType type,
        IReadOnlyList<CandidateGame> candidates,
        UserPreferenceProfile profile,
        CandidateGame? seed,
        RecommendationOptions options)
    {
        var now = DateTimeOffset.UtcNow;
        var becauseSeedTitle = type is RecommendationType.BecauseYouPlayed
            ? "your recent games"
            : null;

        return candidates
            .Select(game =>
            {
                var score = RecommendationScorer.Score(game, profile, options, type, now, seed);
                var reasonSeed = type switch
                {
                    RecommendationType.SimilarGames => seed?.Title,
                    RecommendationType.BecauseYouPlayed => becauseSeedTitle,
                    _ => null
                };

                score = new RecommendationScore
                {
                    Content = score.Content,
                    Preference = score.Preference,
                    Behavior = score.Behavior,
                    Popularity = score.Popularity,
                    Freshness = score.Freshness,
                    Engagement = score.Engagement,
                    Exploration = score.Exploration,
                    Diversity = score.Diversity,
                    Final = score.Final,
                    Reason = RecommendationReasonService.Build(type, game, profile, reasonSeed)
                };

                return new ScoredCandidate { Game = game, Score = score };
            })
            .OrderByDescending(x => x.Score.Final)
            .ThenBy(x => x.Game.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<Guid> BuildExclusions(
        RecommendationType type,
        UserPreferenceProfile profile,
        RecommendationOptions options)
    {
        var exclude = new HashSet<Guid>();
        var excludePlayed = type switch
        {
            RecommendationType.ForYou => options.Exclusion.ExcludePlayedForYou,
            RecommendationType.BecauseYouPlayed => options.Exclusion.ExcludePlayedBecauseYouPlayed,
            RecommendationType.SimilarGames => true,
            _ => false
        };
        var excludeFav = type switch
        {
            RecommendationType.ForYou => options.Exclusion.ExcludeFavoritedForYou,
            RecommendationType.BecauseYouPlayed => options.Exclusion.ExcludeFavoritedBecauseYouPlayed,
            _ => false
        };

        if (excludePlayed)
        {
            foreach (var id in profile.PlayedGameIds) exclude.Add(id);
        }

        if (excludeFav)
        {
            foreach (var id in profile.FavoriteGameIds) exclude.Add(id);
        }

        return exclude;
    }

    private static void InjectExploration(
        List<ScoredCandidate> selected,
        List<ScoredCandidate> ranked,
        int take,
        RecommendationOptions options)
    {
        var slots = Math.Max(1, (int)Math.Round(take * options.ExplorationRatio));
        if (selected.Count < take) return;

        var explorers = ranked
            .Where(x => x.Game.SourceBucket is "hidden" or "new")
            .Where(x => selected.All(s => s.Game.Id != x.Game.Id))
            .Take(slots)
            .ToList();

        if (explorers.Count == 0) return;

        for (var i = 0; i < explorers.Count && selected.Count > 0; i++)
        {
            var replaceIndex = Math.Max(0, selected.Count - 1 - i);
            selected[replaceIndex] = explorers[i];
        }
    }

    private static int takeSafe(int value) => Math.Max(1, value);
}

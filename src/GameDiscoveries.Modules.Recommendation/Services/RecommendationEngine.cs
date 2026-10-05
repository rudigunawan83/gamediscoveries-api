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

    Task<RecommendationHomeResponse> GetHomeAsync(
        Guid? userId,
        int limitPerSection = 12,
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
    IRecommendationTrackingStore tracking,
    IRecommendationModel recommendationModel,
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
        var scored = await RankAsync(type, candidates, profile, seed, options);

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
        var ranked = await RankAsync(type, candidates, profile, seed, options);
        var diversified = DiversityService.Diversify(
            ranked,
            Math.Max(take, take + 5),
            options.MaxSameCategoryInTop10,
            options.Weights.Diversity).ToList();

        InjectExploration(diversified, ranked, take, options);
        var mmr = MmrReranker.Rerank(diversified, take, options.MmrLambda).ToList();

        var strategy = ResolveStrategy(type, profile);
        var items = mmr
            .Take(take)
            .Select((item, index) =>
            {
                var reason = RecommendationReasonService.BuildDetail(type, item.Game, profile,
                    type is RecommendationType.BecauseYouPlayed ? "your recent games" : seed?.Title);
                return new RecommendationItemDto(
                    new RecommendationGameDto(
                        item.Game.Id,
                        item.Game.Slug,
                        item.Game.Title,
                        item.Game.ThumbnailUrl,
                        item.Game.Category,
                        item.Game.Orientation),
                    Math.Round(item.Score.Final * 100d, 2),
                    index + 1,
                    reason.Label,
                    reason,
                    index + 1);
            })
            .ToList();

        // Never return empty when catalog has games — cold-start fallback.
        if (items.Count == 0)
        {
            strategy = "FALLBACK_TRENDING";
            logger.LogWarning("RecommendationFallback type={Type} user={UserId}", type.ToApiValue(), profile.UserId);
            var fallback = await repository.GetTrendingAsync(take, cancellationToken: cancellationToken);
            items = fallback.Select((game, index) => new RecommendationItemDto(
                new RecommendationGameDto(game.Id, game.Slug, game.Title, game.ThumbnailUrl, game.Category, game.Orientation),
                Math.Round(50d - (index * 1d), 2),
                index + 1,
                "Trending now",
                new RecommendationReasonDto("FALLBACK", "Trending now"),
                index + 1)).ToList();
        }

        var requestId = await tracking.CreateRequestAsync(
            profile.UserId,
            null,
            strategy,
            type.ToApiValue(),
            profile.ProfileLevel,
            candidates.Count,
            items.Count,
            options.AlgorithmVersion,
            cancellationToken);

        var response = new RecommendationResponse(
            items,
            type.ToApiValue(),
            options.AlgorithmVersion,
            now,
            now,
            false,
            strategy,
            profile.ProfileLevel,
            requestId);

        return (response, candidates.Count, profile.IsColdStart);
    }

    public async Task<RecommendationHomeResponse> GetHomeAsync(
        Guid? userId,
        int limitPerSection = 12,
        CancellationToken cancellationToken = default)
    {
        var sections = new List<RecommendationHomeSectionDto>();
        var forYou = await GetAsync(RecommendationType.ForYou, userId, null, limitPerSection, true, cancellationToken);
        if (forYou.Items.Count > 0)
        {
            sections.Add(new RecommendationHomeSectionDto("FOR_YOU", "Recommended For You", forYou.Items));
        }

        var because = await GetAsync(RecommendationType.BecauseYouPlayed, userId, null, limitPerSection, true, cancellationToken);
        if (because.Items.Count > 0 && userId is not null)
        {
            sections.Add(new RecommendationHomeSectionDto("BECAUSE_YOU_PLAYED", "Because You Played", because.Items));
        }

        var trending = await GetAsync(RecommendationType.Trending, userId, null, limitPerSection, true, cancellationToken);
        if (trending.Items.Count > 0)
        {
            sections.Add(new RecommendationHomeSectionDto("TRENDING_FOR_YOU", "Trending For You", trending.Items));
        }

        var newest = await GetAsync(RecommendationType.NewDiscoveries, userId, null, limitPerSection, true, cancellationToken);
        if (newest.Items.Count > 0)
        {
            sections.Add(new RecommendationHomeSectionDto("NEW_FOR_YOU", "New Games You Might Like", newest.Items));
        }

        if (userId is not null)
        {
            var profileForFavorites = await repository.BuildProfileAsync(userId, cancellationToken);
            var favoriteSeedId = profileForFavorites.FavoriteGameIds.FirstOrDefault();
            if (favoriteSeedId != Guid.Empty)
            {
                var favorites = await GetAsync(
                    RecommendationType.SimilarGames, userId, favoriteSeedId, limitPerSection, true, cancellationToken);
                if (favorites.Items.Count > 0)
                {
                    sections.Add(new RecommendationHomeSectionDto(
                        "BASED_ON_FAVORITES", "Based On Your Favorites", favorites.Items));
                }
            }
        }

        var explore = await GetAsync(RecommendationType.HiddenGems, userId, null, limitPerSection, true, cancellationToken);
        if (explore.Items.Count > 0)
        {
            sections.Add(new RecommendationHomeSectionDto("EXPLORATION", "Explore Something Different", explore.Items));
        }

        var options = optionsAccessor.Value;
        var profile = await repository.BuildProfileAsync(userId, cancellationToken);
        var requestId = await tracking.CreateRequestAsync(
            userId, null, "HOME", "home", profile.ProfileLevel, 0,
            sections.Sum(s => s.Items.Count), options.AlgorithmVersion, cancellationToken);

        return new RecommendationHomeResponse(sections, options.AlgorithmVersion, profile.ProfileLevel, requestId);
    }

    private static string ResolveStrategy(RecommendationType type, UserPreferenceProfile profile) =>
        type switch
        {
            RecommendationType.BecauseYouPlayed => "BECAUSE_YOU_PLAYED",
            RecommendationType.Trending => profile.IsColdStart ? "TRENDING_V1" : "TRENDING_FOR_YOU",
            RecommendationType.NewDiscoveries => "NEW_FOR_YOU",
            RecommendationType.HiddenGems => "EXPLORATION",
            RecommendationType.SimilarGames => "FAVORITE_SIMILAR",
            _ => profile.IsColdStart ? "COLD_START" : "PERSONALIZED"
        };

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

    private async Task<List<ScoredCandidate>> RankAsync(
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

        var scored = await recommendationModel.ScoreCandidatesAsync(
            new RecommendationContext(type, profile, options, now, seed),
            candidates);

        return scored
            .Select(item =>
            {
                var reasonSeed = type switch
                {
                    RecommendationType.SimilarGames => seed?.Title,
                    RecommendationType.BecauseYouPlayed => becauseSeedTitle,
                    _ => null
                };

                var reason = RecommendationReasonService.BuildDetail(type, item.Game, profile, reasonSeed);
                var score = item.Score;
                score = new RecommendationScore
                {
                    Content = score.Content,
                    Preference = score.Preference,
                    Behavior = score.Behavior,
                    Popularity = score.Popularity,
                    Freshness = score.Freshness,
                    Engagement = score.Engagement,
                    Exploration = score.Exploration,
                    Discovery = score.Discovery,
                    Trending = score.Trending,
                    Novelty = score.Novelty,
                    Diversity = score.Diversity,
                    Final = score.Final,
                    Reason = reason.Label,
                    ReasonType = reason.Type
                };

                return new ScoredCandidate { Game = item.Game, Score = score };
            })
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

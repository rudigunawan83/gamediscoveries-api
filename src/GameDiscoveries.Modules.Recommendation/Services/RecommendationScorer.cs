using GameDiscoveries.Modules.Recommendation.Configuration;
using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.Modules.Recommendation.Services;

public static class RecommendationScorer
{
    public static RecommendationScore Score(
        CandidateGame game,
        UserPreferenceProfile profile,
        RecommendationOptions options,
        RecommendationType type,
        DateTimeOffset now,
        CandidateGame? seed = null)
    {
        var content = ComputeContentScore(game, profile, options.ContentWeights, seed);
        var preference = ComputePreferenceScore(game, profile);
        var behavior = ComputeBehaviorScore(game, profile, options.TimeDecay, now);
        var popularity = NormalizeProxy(game.PopularityProxy, 8);
        var freshness = game.FreshnessScore > 0
            ? Clamp01(game.FreshnessScore / 100d)
            : ComputeFreshness(game.PublishedAt, now);
        var engagement = NormalizeProxy(game.EngagementProxy, 3600);
        var exploration = ComputeExploration(game, popularity);
        var discovery = Clamp01(game.DiscoveryScore / 100d);
        var trending = Clamp01(game.TrendingScore / 100d);
        var novelty = ComputeNovelty(game, profile, freshness);

        if (type is RecommendationType.HiddenGems)
        {
            popularity = Math.Max(0, 1 - popularity);
        }

        // Phase 09 blend: keep legacy weights but inject Discovery/Trending/Novelty.
        var weights = options.Weights;
        var personalRelevance = Clamp01((preference * 0.55) + (behavior * 0.30) + (content * 0.15));
        var final =
            (personalRelevance * 0.30)
            + (content * 0.15)
            + (preference * 0.10)
            + (discovery * 0.10)
            + (trending * 0.05)
            + (freshness * 0.05)
            + (novelty * 0.05)
            + (engagement * 0.05)
            + (exploration * 0.05)
            + (behavior * 0.05)
            + (popularity * weights.Popularity * 0.5);

        // Soft repeat-play penalty (favorites less penalized)
        var lastPlayed = profile.Signals
            .Where(s => s.GameId == game.Id && s.Kind is "played" or "completed")
            .Select(s => (DateTimeOffset?)s.At)
            .OrderByDescending(x => x)
            .FirstOrDefault();
        var isFavorite = profile.FavoriteGameIds.Contains(game.Id);
        final = Clamp01(final * (1 - PreferenceSignals.RepeatPlayPenalty(lastPlayed, now, isFavorite)));

        if (profile.DislikedGameIds.Contains(game.Id))
        {
            final = 0;
        }

        if (type is RecommendationType.HiddenGems)
        {
            final = Clamp01((engagement * 0.45) + (freshness * 0.25) + (content * 0.20) + (exploration * 0.20) - (NormalizeProxy(game.PopularityProxy, 8) * 0.35));
        }

        return new RecommendationScore
        {
            Content = Clamp01(content),
            Preference = Clamp01(preference),
            Behavior = Clamp01(behavior),
            Popularity = Clamp01(popularity),
            Freshness = Clamp01(freshness),
            Engagement = Clamp01(engagement),
            Exploration = Clamp01(exploration),
            Discovery = discovery,
            Trending = trending,
            Novelty = novelty,
            Diversity = 0,
            Final = Clamp01(final),
            Reason = string.Empty
        };
    }

    private static double ComputeNovelty(
        CandidateGame game,
        UserPreferenceProfile profile,
        double freshness)
    {
        var dominant = profile.PreferredCategories.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
        var outside = !string.IsNullOrWhiteSpace(dominant)
                      && !string.Equals(game.Category, dominant, StringComparison.OrdinalIgnoreCase);
        var notPlayed = !profile.PlayedGameIds.Contains(game.Id);
        return Clamp01((outside ? 0.45 : 0.15) + (notPlayed ? 0.25 : 0) + (freshness * 0.30));
    }

    public static double ComputeContentScore(
        CandidateGame game,
        UserPreferenceProfile profile,
        ContentSimilarityWeights weights,
        CandidateGame? seed = null)
    {
        if (seed is not null)
        {
            return ComputePairSimilarity(game, seed, weights);
        }

        var category = ScoreKeyMatch(game.Category, profile.PreferredCategories);
        var tags = ScoreTagOverlap(game.Tags, profile.PreferredTags);
        var orientation = ScoreKeyMatch(game.Orientation, profile.PreferredOrientations);
        var mobile = game.MobileReady ? profile.MobilePreference : 1 - Math.Min(profile.MobilePreference, 1);
        var multiplayer = game.Multiplayer
            ? profile.MultiplayerPreference
            : 1 - Math.Min(profile.MultiplayerPreference, 1);
        var description = 0d;

        return Clamp01(
            (category * weights.Category)
            + (tags * weights.Tag)
            + (0 * weights.GameType)
            + (orientation * weights.Orientation)
            + (Clamp01(mobile) * weights.Mobile)
            + (Clamp01(multiplayer) * weights.Multiplayer)
            + (description * weights.Description));
    }

    public static double ComputePairSimilarity(
        CandidateGame left,
        CandidateGame right,
        ContentSimilarityWeights weights)
    {
        var category = string.Equals(left.Category, right.Category, StringComparison.OrdinalIgnoreCase) ? 1d : 0d;
        var tags = ScoreTagOverlap(left.Tags, right.Tags.ToDictionary(t => t, _ => 1d, StringComparer.OrdinalIgnoreCase));
        var orientation = string.Equals(left.Orientation, right.Orientation, StringComparison.OrdinalIgnoreCase) ? 1d : 0d;
        var mobile = left.MobileReady == right.MobileReady ? 1d : 0d;
        var multiplayer = left.Multiplayer == right.Multiplayer ? 1d : 0d;
        var description = DescriptionOverlap(left.Description, right.Description);

        return Clamp01(
            (category * weights.Category)
            + (tags * weights.Tag)
            + (0 * weights.GameType)
            + (orientation * weights.Orientation)
            + (mobile * weights.Mobile)
            + (multiplayer * weights.Multiplayer)
            + (description * weights.Description));
    }

    private static double ComputePreferenceScore(CandidateGame game, UserPreferenceProfile profile)
    {
        if (profile.IsColdStart) return 0;
        var category = ScoreKeyMatch(game.Category, profile.PreferredCategories);
        var tags = ScoreTagOverlap(game.Tags, profile.PreferredTags);
        return Clamp01((category * 0.6) + (tags * 0.4));
    }

    private static double ComputeBehaviorScore(
        CandidateGame game,
        UserPreferenceProfile profile,
        TimeDecayOptions decay,
        DateTimeOffset now)
    {
        var related = profile.Signals
            .Where(s =>
                string.Equals(s.Category, game.Category, StringComparison.OrdinalIgnoreCase)
                || s.Tags.Any(t => game.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        if (related.Count == 0) return 0;

        var weighted = related.Select(s =>
        {
            var kindWeight = s.Kind switch
            {
                "favorite" => 1.0,
                "completed" => 0.9,
                "played" => 0.7,
                "viewed" => 0.3,
                "clicked" => 0.2,
                _ => 0.4
            };

            if (s.Kind == "played" && s.DurationSeconds > 0 && s.DurationSeconds < 15)
            {
                kindWeight = 0.1;
            }

            return kindWeight
                   * TimeDecay.Compute(s.At, now, decay)
                   * TimeDecay.InteractionConfidence(s.StrengthCount);
        });

        return Clamp01(weighted.Average());
    }

    private static double ComputeFreshness(DateTimeOffset? publishedAt, DateTimeOffset now)
    {
        if (publishedAt is null) return 0.2;
        var days = Math.Max(0, (now - publishedAt.Value).TotalDays);
        if (days <= 7) return 1;
        if (days <= 30) return 0.75;
        if (days <= 90) return 0.5;
        if (days <= 180) return 0.3;
        return 0.15;
    }

    private static double ComputeExploration(CandidateGame game, double popularity)
        => Clamp01((1 - popularity) * 0.7 + (game.GlobalPlaySessions <= 3 ? 0.3 : 0));

    private static double ScoreKeyMatch(string? key, IReadOnlyDictionary<string, double> map)
    {
        if (string.IsNullOrWhiteSpace(key)) return 0;
        return map.TryGetValue(key, out var score) ? Clamp01(score) : 0;
    }

    private static double ScoreTagOverlap(IReadOnlyList<string> tags, IReadOnlyDictionary<string, double> preferred)
    {
        if (tags.Count == 0 || preferred.Count == 0) return 0;
        var hits = tags.Where(preferred.ContainsKey).Select(t => preferred[t]).ToList();
        if (hits.Count == 0) return 0;
        return Clamp01(hits.Average());
    }

    private static double DescriptionOverlap(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        var left = Tokenize(a);
        var right = Tokenize(b);
        if (left.Count == 0 || right.Count == 0) return 0;
        var overlap = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        return Clamp01(overlap / (double)Math.Max(left.Count, right.Count));
    }

    private static HashSet<string> Tokenize(string value)
        => value
            .Split([' ', ',', '.', '!', '?', ';', ':', '/', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 2)
            .Take(40)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static double NormalizeProxy(double value, double scale)
        => Clamp01(value <= 0 ? 0 : value / scale);

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}

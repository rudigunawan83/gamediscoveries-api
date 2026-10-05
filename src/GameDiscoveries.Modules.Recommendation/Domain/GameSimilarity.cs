namespace GameDiscoveries.Modules.Recommendation.Domain;

public static class GameSimilarity
{
    public static double Jaccard(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var a = left.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToHashSet();
        var b = right.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToHashSet();
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    public static double Calculate(
        CandidateGame a,
        CandidateGame b,
        double genreWeight = 0.40,
        double categoryWeight = 0.20,
        double tagWeight = 0.30,
        double qualityWeight = 0.10)
    {
        var category = string.Equals(a.Category, b.Category, StringComparison.OrdinalIgnoreCase) ? 1d : 0d;
        // Genre approximated by category when dedicated genres unavailable
        var genre = category;
        var tags = Jaccard(a.Tags, b.Tags);
        var quality = Math.Clamp(((a.DiscoveryScore + b.DiscoveryScore) / 200d), 0, 1);
        return Math.Clamp(
            (genre * genreWeight) +
            (category * categoryWeight) +
            (tags * tagWeight) +
            (quality * qualityWeight),
            0,
            1) * 100d;
    }
}

public static class PreferenceSignals
{
    public static double RatingPreference(int rating) => (rating - 3) / 2d;

    public static double RecencyDecay(DateTimeOffset at, DateTimeOffset now, double decayDays)
    {
        var ageDays = Math.Max(0, (now - at).TotalDays);
        var decay = Math.Max(1d, decayDays);
        return Math.Exp(-ageDays / decay);
    }

    public static int ResolveProfileLevel(int totalInteractions) =>
        totalInteractions switch
        {
            <= 0 => 0,
            <= 2 => 1,
            <= 9 => 2,
            <= 49 => 3,
            _ => 4
        };

    public static double RepeatPlayPenalty(DateTimeOffset? lastPlayedAt, DateTimeOffset now, bool isFavorite)
    {
        if (lastPlayedAt is null)
        {
            return 0;
        }

        var days = (now - lastPlayedAt.Value).TotalDays;
        var penalty = days switch
        {
            <= 1 => 0.50,
            <= 3 => 0.30,
            <= 7 => 0.15,
            <= 30 => 0.05,
            _ => 0.0
        };

        return isFavorite ? penalty * 0.4 : penalty;
    }
}

public static class MmrReranker
{
    public static IReadOnlyList<ScoredCandidate> Rerank(
        IReadOnlyList<ScoredCandidate> ranked,
        int limit,
        double lambda)
    {
        lambda = Math.Clamp(lambda, 0.5, 0.95);
        var remaining = ranked.ToList();
        var selected = new List<ScoredCandidate>();

        while (selected.Count < limit && remaining.Count > 0)
        {
            ScoredCandidate? best = null;
            var bestScore = double.MinValue;

            foreach (var candidate in remaining)
            {
                var maxSim = selected.Count == 0
                    ? 0
                    : selected.Max(s =>
                        GameSimilarity.Jaccard(s.Game.Tags, candidate.Game.Tags) * 0.7
                        + (string.Equals(s.Game.Category, candidate.Game.Category, StringComparison.OrdinalIgnoreCase) ? 0.3 : 0));

                var mmr = (lambda * candidate.Score.Final) - ((1 - lambda) * maxSim);
                if (mmr > bestScore)
                {
                    bestScore = mmr;
                    best = candidate;
                }
            }

            if (best is null)
            {
                break;
            }

            selected.Add(best);
            remaining.Remove(best);
        }

        return selected;
    }
}

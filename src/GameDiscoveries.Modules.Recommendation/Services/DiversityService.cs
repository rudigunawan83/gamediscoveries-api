using GameDiscoveries.Modules.Recommendation.Domain;

namespace GameDiscoveries.Modules.Recommendation.Services;

public static class DiversityService
{
    public static IReadOnlyList<ScoredCandidate> Diversify(
        IEnumerable<ScoredCandidate> ranked,
        int limit,
        int maxSameCategoryInTop10,
        double diversityWeight)
    {
        var remaining = ranked
            .OrderByDescending(x => x.Score.Final)
            .ThenBy(x => x.Game.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = new List<ScoredCandidate>(limit);
        var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        while (selected.Count < limit && remaining.Count > 0)
        {
            ScoredCandidate? best = null;
            var bestAdjusted = double.MinValue;

            foreach (var candidate in remaining)
            {
                var category = candidate.Game.Category ?? string.Empty;
                categoryCounts.TryGetValue(category, out var count);

                var inTop10Window = selected.Count < 10;
                if (inTop10Window
                    && !string.IsNullOrWhiteSpace(category)
                    && count >= maxSameCategoryInTop10)
                {
                    continue;
                }

                var penalty = count * 0.08 * Math.Clamp(diversityWeight / 0.05, 0.5, 2.0);
                var adjusted = candidate.Score.Final - penalty;
                if (adjusted > bestAdjusted)
                {
                    bestAdjusted = adjusted;
                    best = candidate;
                }
            }

            if (best is null)
            {
                // Never break the top-10 category cap; stop early if no diverse candidate remains.
                if (selected.Count < 10)
                {
                    break;
                }

                best = remaining[0];
            }

            selected.Add(best);
            remaining.Remove(best);

            var selectedCategory = best.Game.Category ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selectedCategory))
            {
                categoryCounts[selectedCategory] = categoryCounts.TryGetValue(selectedCategory, out var c)
                    ? c + 1
                    : 1;
            }
        }

        return selected;
    }
}

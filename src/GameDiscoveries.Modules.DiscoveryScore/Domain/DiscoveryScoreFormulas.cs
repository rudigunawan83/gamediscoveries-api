namespace GameDiscoveries.Modules.DiscoveryScore.Domain;

public static class MetricNormalization
{
    public static double Log1p(double value) => Math.Log(1d + Math.Max(0, value));

    public static double MinMax(double value, double min, double max)
    {
        if (Math.Abs(max - min) < 1e-9)
        {
            return value > 0 ? 100 : 0;
        }

        return Clamp(((value - min) / (max - min)) * 100d);
    }

    public static double NormalizeLog1pMinMax(IReadOnlyList<double> values, double value)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var transformed = values.Select(Log1p).ToList();
        return MinMax(Log1p(value), transformed.Min(), transformed.Max());
    }

    public static double NormalizePercentile(IReadOnlyList<double> values, double value)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(v => v).ToList();
        var rank = sorted.Count(v => v <= value);
        return Clamp(100d * rank / sorted.Count);
    }

    public static double NormalizeGrowth(double current, double previous, double smoothing)
    {
        var s = Math.Max(0.1, smoothing);
        var rate = ((current + s) / (previous + s)) - 1d;
        // Map growth rate into 0..100 around neutral 50
        var mapped = 50d + (Math.Tanh(rate) * 50d);
        return Clamp(mapped);
    }

    public static double Clamp(double value) => Math.Clamp(value, 0d, 100d);
}

public static class DiscoveryScoreFormulas
{
    public static bool ValidateWeights(params double[] weights) =>
        Math.Abs(weights.Sum() - 1.0) < 0.001;

    public static double BayesianRating(double averageRating, int ratingCount, double m, double globalAverage)
    {
        var v = Math.Max(0, ratingCount);
        var mm = Math.Max(0.1, m);
        return ((v / (v + mm)) * averageRating) + ((mm / (v + mm)) * globalAverage);
    }

    public static double FreshnessScore(double ageDays, double decayConstant)
    {
        var decay = Math.Max(1d, decayConstant);
        return MetricNormalization.Clamp(Math.Exp(-Math.Max(0, ageDays) / decay) * 100d);
    }

    public static double PopularityScore(
        double nViews, double nStarts, double nValidSessions, double nUniqueUsers) =>
        MetricNormalization.Clamp(
            0.25 * nViews +
            0.25 * nStarts +
            0.30 * nValidSessions +
            0.20 * nUniqueUsers);

    public static double EngagementScore(
        double nAvgSession, double nActiveTime, double nReturning,
        double nSessionsPerUser, double nValidRate) =>
        MetricNormalization.Clamp(
            0.30 * nAvgSession +
            0.25 * nActiveTime +
            0.20 * nReturning +
            0.15 * nSessionsPerUser +
            0.10 * nValidRate);

    public static double QualityScore(
        double bayesianRating01to5, double nFavoriteRate, double nReviewRate)
    {
        var ratingNorm = MetricNormalization.Clamp((bayesianRating01to5 / 5d) * 100d);
        return MetricNormalization.Clamp(
            0.60 * ratingNorm +
            0.25 * nFavoriteRate +
            0.15 * nReviewRate);
    }

    public static double GrowthScore(
        double viewGrowth, double startGrowth, double uniqueGrowth,
        double sessionGrowth, double favoriteGrowth) =>
        MetricNormalization.Clamp(
            0.25 * viewGrowth +
            0.25 * startGrowth +
            0.25 * uniqueGrowth +
            0.15 * sessionGrowth +
            0.10 * favoriteGrowth);

    public static double DiscoveryScore(
        double popularity, double engagement, double quality,
        double momentum, double growth, double freshness,
        double wPop, double wEng, double wQual, double wMom, double wGrow, double wFresh) =>
        MetricNormalization.Clamp(
            popularity * wPop +
            engagement * wEng +
            quality * wQual +
            momentum * wMom +
            growth * wGrow +
            freshness * wFresh);

    public static double TrendingScore(
        double recentActivity, double momentum, double growth, double engagement, double freshness,
        double wRecent, double wMom, double wGrow, double wEng, double wFresh) =>
        MetricNormalization.Clamp(
            recentActivity * wRecent +
            momentum * wMom +
            growth * wGrow +
            engagement * wEng +
            freshness * wFresh);

    public static string ResolveTrendState(
        double growthPercent,
        double trendingScore,
        bool isNew,
        double risingThreshold,
        double decliningThreshold)
    {
        if (isNew)
        {
            return DiscoveryTrendStates.New;
        }

        if (growthPercent >= risingThreshold)
        {
            return DiscoveryTrendStates.Rising;
        }

        if (growthPercent <= decliningThreshold)
        {
            return DiscoveryTrendStates.Declining;
        }

        if (trendingScore >= 75)
        {
            return DiscoveryTrendStates.Hot;
        }

        return DiscoveryTrendStates.Stable;
    }

    public static string ImpactLabel(double score) =>
        score >= 75 ? "HIGH" : score >= 45 ? "MEDIUM" : "LOW";
}

public static class DiscoveryTrendStates
{
    public const string Rising = "RISING";
    public const string Hot = "HOT";
    public const string Stable = "STABLE";
    public const string Declining = "DECLINING";
    public const string New = "NEW";
}

public static class DiscoveryWindowTypes
{
    public const string Hourly = "HOURLY";
    public const string Daily = "DAILY";
    public const string Weekly = "WEEKLY";
    public const string Monthly = "MONTHLY";
}

public static class DiscoveryRankingTypes
{
    public const string Trending = "TRENDING";
    public const string Rising = "RISING";
    public const string Popular = "POPULAR";
    public const string MostPlayed = "MOST_PLAYED";
    public const string MostFavorited = "MOST_FAVORITED";
    public const string MostRated = "MOST_RATED";
    public const string MostReviewed = "MOST_REVIEWED";
    public const string NewTrending = "NEW_TRENDING";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Trending, Rising, Popular, MostPlayed, MostFavorited, MostRated, MostReviewed, NewTrending
    };
}

public static class DiscoveryPeriodTypes
{
    public const string Hour = "HOUR";
    public const string Day = "DAY";
    public const string Week = "WEEK";
}

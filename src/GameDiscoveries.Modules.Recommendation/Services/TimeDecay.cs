using GameDiscoveries.Modules.Recommendation.Configuration;

namespace GameDiscoveries.Modules.Recommendation.Services;

public static class TimeDecay
{
    public static double Compute(DateTimeOffset at, DateTimeOffset now, TimeDecayOptions options)
    {
        var days = Math.Max(0, (now - at).TotalDays);
        if (days <= 0) return Clamp01(options.Day0);
        if (days <= 3) return Clamp01(Lerp(options.Day0, options.Day3, days / 3d));
        if (days <= 7) return Clamp01(Lerp(options.Day3, options.Day7, (days - 3) / 4d));
        if (days <= 14) return Clamp01(Lerp(options.Day7, options.Day14, (days - 7) / 7d));
        if (days <= 30) return Clamp01(Lerp(options.Day14, options.Day30, (days - 14) / 16d));
        if (days <= 60) return Clamp01(Lerp(options.Day30, options.Day60Plus, (days - 30) / 30d));
        return Clamp01(options.Day60Plus);
    }

    public static double InteractionConfidence(int interactions)
        => interactions switch
        {
            <= 0 => 0,
            1 => 0.3,
            2 => 0.5,
            3 => 0.7,
            4 => 0.85,
            _ => 1.0
        };

    private static double Lerp(double a, double b, double t) => a + ((b - a) * Math.Clamp(t, 0, 1));

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}

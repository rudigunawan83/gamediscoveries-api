using System.Diagnostics.Metrics;

namespace GameDiscoveries.Infrastructure.Providers.Observability;

public static class ProviderSyncMetrics
{
    public const string MeterName = "GameDiscoveries.Providers";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> GamesFetched = Meter.CreateCounter<long>(
        "gamemonetize_games_fetched",
        description: "Games fetched from GameMonetize feeds");

    private static readonly Counter<long> GamesCreated = Meter.CreateCounter<long>(
        "gamemonetize_games_created",
        description: "Games created during GameMonetize sync");

    private static readonly Counter<long> GamesUpdated = Meter.CreateCounter<long>(
        "gamemonetize_games_updated",
        description: "Games updated during GameMonetize sync");

    private static readonly Counter<long> GamesFailed = Meter.CreateCounter<long>(
        "gamemonetize_games_failed",
        description: "Game items that failed during GameMonetize sync");

    private static readonly Counter<long> GamesUnavailable = Meter.CreateCounter<long>(
        "gamemonetize_games_unavailable",
        description: "Games marked unavailable after GameMonetize sync");

    private static readonly Counter<long> ApiErrors = Meter.CreateCounter<long>(
        "gamemonetize_api_errors",
        description: "GameMonetize API / sync systemic errors");

    private static readonly Histogram<double> SyncDuration = Meter.CreateHistogram<double>(
        "gamemonetize_sync_duration",
        unit: "ms",
        description: "GameMonetize sync duration in milliseconds");

    public static void Record(
        string feedType,
        int fetched,
        int created,
        int updated,
        int failed,
        int unavailable,
        long durationMs,
        bool systemicError)
    {
        var tags = new KeyValuePair<string, object?>("feed_type", feedType);
        GamesFetched.Add(fetched, tags);
        GamesCreated.Add(created, tags);
        GamesUpdated.Add(updated, tags);
        GamesFailed.Add(failed, tags);
        GamesUnavailable.Add(unavailable, tags);
        SyncDuration.Record(durationMs, tags);

        if (systemicError)
        {
            ApiErrors.Add(1, tags);
        }
    }
}

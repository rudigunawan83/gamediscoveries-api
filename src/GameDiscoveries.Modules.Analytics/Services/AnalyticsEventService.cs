using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Data;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Options;
using GameDiscoveries.Modules.Analytics.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Analytics.Services;

public interface IAnalyticsEventService
{
    Task<AnalyticsEventIngestResult> TrackAsync(
        AnalyticsEventIngestRequest request,
        Guid? authenticatedUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AnalyticsBatchIngestResult> TrackBatchAsync(
        IReadOnlyList<AnalyticsEventIngestRequest> events,
        Guid? authenticatedUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
}

public sealed class AnalyticsEventService(
    IAnalyticsEventStore store,
    IAnalyticsEventValidator validator,
    IAnalyticsEventDispatcher dispatcher,
    IOptions<AnalyticsOptions> options,
    ILogger<AnalyticsEventService> logger) : IAnalyticsEventService
{
    public async Task<AnalyticsEventIngestResult> TrackAsync(
        AnalyticsEventIngestRequest request,
        Guid? authenticatedUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            return new AnalyticsEventIngestResult(request.EventId ?? Guid.Empty, "rejected", "Analytics disabled.");
        }

        var receivedAt = DateTimeOffset.UtcNow;
        var command = validator.ValidateAndNormalize(
            request,
            authenticatedUserId,
            ipAddress,
            userAgent,
            receivedAt);

        if (await store.ExistsAsync(command.EventId, cancellationToken))
        {
            logger.LogInformation(
                "analytics_event_duplicate eventType={EventType} eventId={EventId}",
                command.EventType,
                command.EventId);
            return new AnalyticsEventIngestResult(command.EventId, "duplicate");
        }

        await EnsureGameExistsAsync(command.GameId, opts, cancellationToken);
        var inserted = await PersistAsync(command, cancellationToken);
        if (!inserted)
        {
            logger.LogInformation(
                "analytics_event_duplicate eventType={EventType} eventId={EventId}",
                command.EventType,
                command.EventId);
            return new AnalyticsEventIngestResult(command.EventId, "duplicate");
        }

        logger.LogInformation(
            "analytics_event_received eventType={EventType} eventId={EventId} gameId={GameId} source={Source}",
            command.EventType,
            command.EventId,
            command.GameId,
            command.Source);

        return new AnalyticsEventIngestResult(command.EventId, "accepted");
    }

    public async Task<AnalyticsBatchIngestResult> TrackBatchAsync(
        IReadOnlyList<AnalyticsEventIngestRequest> events,
        Guid? authenticatedUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            throw new ValidationException("Analytics is disabled.");
        }

        if (events.Count == 0)
        {
            throw new ValidationException("events must not be empty.");
        }

        if (events.Count > opts.MaxBatchSize)
        {
            throw new ValidationException(
                $"Batch exceeds limit of {opts.MaxBatchSize} events.",
                new Dictionary<string, string[]>
                {
                    ["events"] = [$"Maximum {opts.MaxBatchSize} events per request."]
                });
        }

        var receivedAt = DateTimeOffset.UtcNow;
        var results = new List<AnalyticsEventIngestResult>(events.Count);
        var acceptedCommands = new List<AnalyticsEventWriteCommand>();
        var seenInBatch = new HashSet<Guid>();

        // Preload duplicates and games for efficiency.
        var candidateIds = events
            .Select(e => e.EventId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var existingIds = await store.GetExistingEventIdsAsync(candidateIds, cancellationToken);

        var gameIds = events
            .Where(e => e.GameId is not null && e.GameId != Guid.Empty)
            .Select(e => e.GameId!.Value)
            .Distinct()
            .ToArray();
        var existingGames = opts.ValidateGameExists
            ? await store.GetExistingGameIdsAsync(gameIds, cancellationToken)
            : gameIds.ToHashSet();

        foreach (var request in events)
        {
            try
            {
                var command = validator.ValidateAndNormalize(
                    request,
                    authenticatedUserId,
                    ipAddress,
                    userAgent,
                    receivedAt);

                if (!seenInBatch.Add(command.EventId) || existingIds.Contains(command.EventId))
                {
                    results.Add(new AnalyticsEventIngestResult(command.EventId, "duplicate"));
                    continue;
                }

                if (command.GameId is not null &&
                    opts.ValidateGameExists &&
                    !existingGames.Contains(command.GameId.Value))
                {
                    results.Add(new AnalyticsEventIngestResult(
                        command.EventId,
                        "rejected",
                        $"Game '{command.GameId}' was not found."));
                    continue;
                }

                acceptedCommands.Add(command);
                results.Add(new AnalyticsEventIngestResult(command.EventId, "accepted"));
            }
            catch (ValidationException ex)
            {
                results.Add(new AnalyticsEventIngestResult(
                    request.EventId ?? Guid.Empty,
                    "rejected",
                    ex.Detail));
            }
        }

        if (acceptedCommands.Count > 0)
        {
            await store.InsertManyAsync(acceptedCommands, cancellationToken);

            foreach (var command in acceptedCommands)
            {
                await AfterPersistAsync(command, cancellationToken);
            }

            logger.LogInformation(
                "analytics_batch_received accepted={Accepted} duplicates={Duplicates} rejected={Rejected}",
                acceptedCommands.Count,
                results.Count(r => r.Status == "duplicate"),
                results.Count(r => r.Status == "rejected"));
        }

        return new AnalyticsBatchIngestResult(
            results.Count(r => r.Status == "accepted"),
            results.Count(r => r.Status == "duplicate"),
            results.Count(r => r.Status == "rejected"),
            results);
    }

    public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        store.ExistsAsync(eventId, cancellationToken);

    public Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        store.GetOverviewAsync(cancellationToken);

    private async Task EnsureGameExistsAsync(
        Guid? gameId,
        AnalyticsOptions opts,
        CancellationToken cancellationToken)
    {
        if (gameId is null || !opts.ValidateGameExists)
        {
            return;
        }

        if (!await store.GameExistsAsync(gameId.Value, cancellationToken))
        {
            throw new NotFoundException("Game", $"Game '{gameId}' was not found.");
        }
    }

    private async Task<bool> PersistAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken)
    {
        var inserted = await store.TryInsertAsync(command, cancellationToken);
        if (inserted == 0)
        {
            return false;
        }

        await AfterPersistAsync(command, cancellationToken);
        return true;
    }

    private async Task AfterPersistAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.SessionId))
        {
            await store.UpsertSessionAsync(
                command.SessionId,
                command.UserId,
                command.AnonymousId,
                command.Source,
                command.Platform,
                command.OccurredAt,
                cancellationToken);
        }

        if (command.UserId is not null && command.AnonymousId is not null)
        {
            await store.LinkIdentityAsync(command.AnonymousId.Value, command.UserId.Value, cancellationToken);
        }

        // Phase 01: dispatch is intentionally empty (no handlers registered yet).
        await dispatcher.DispatchAsync(command, cancellationToken);
    }
}

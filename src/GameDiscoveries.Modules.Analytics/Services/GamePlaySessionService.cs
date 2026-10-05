using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Data;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Analytics.Services;

public interface IGamePlaySessionService
{
    Task<GamePlaySessionResponse> StartAsync(
        Guid gameId,
        StartGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        CancellationToken cancellationToken = default);

    Task<GamePlaySessionResponse> HeartbeatAsync(
        string sessionId,
        HeartbeatGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default);

    Task<GamePlaySessionResponse> PauseAsync(
        string sessionId,
        PauseGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default);

    Task<GamePlaySessionResponse> ResumeAsync(
        string sessionId,
        ResumeGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default);

    Task<GamePlaySessionResponse> EndAsync(
        string sessionId,
        EndGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default);

    Task<GamePlaySessionResponse> GetAsync(
        string sessionId,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default);

    Task<GamePlaySessionOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
}

public sealed class GamePlaySessionService(
    IGamePlaySessionStore store,
    IAnalyticsEventService analytics,
    IOptions<GameSessionOptions> options,
    ILogger<GamePlaySessionService> logger) : IGamePlaySessionService
{
    public async Task<GamePlaySessionResponse> StartAsync(
        Guid gameId,
        StartGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            throw new ValidationException("Game session tracking is disabled.");
        }

        if (gameId == Guid.Empty)
        {
            throw new ValidationException("gameId is required.");
        }

        if (!await store.GameIsPublishedAsync(gameId, cancellationToken))
        {
            throw new NotFoundException("Game", $"Game '{gameId}' was not found or is not active.");
        }

        if (authenticatedUserId is null &&
            (request.AnonymousId is null || request.AnonymousId == Guid.Empty))
        {
            throw new ValidationException(
                "AnonymousId is required for unauthenticated sessions.",
                new Dictionary<string, string[]> { ["anonymousId"] = ["AnonymousId is required."] });
        }

        var sessionId = (request.SessionId ?? Guid.NewGuid()).ToString("D");
        var existing = await store.GetBySessionIdAsync(sessionId, cancellationToken);
        if (existing is not null)
        {
            EnsureOwnership(existing, authenticatedUserId, request.AnonymousId);
            logger.LogInformation(
                "duplicate_session sessionId={SessionId} gameId={GameId}",
                sessionId,
                gameId);
            return ToResponse(existing);
        }

        if (!opts.AllowMultipleSameGameSessions)
        {
            var open = await store.GetOpenSessionsForIdentityAndGameAsync(
                authenticatedUserId,
                request.AnonymousId,
                gameId,
                cancellationToken);

            foreach (var prior in open)
            {
                await FinalizeAsInvalidAsync(
                    prior,
                    GamePlaySessionInvalidReasons.DuplicateSession,
                    GamePlayEndReasons.Timeout,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new GamePlaySessionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = authenticatedUserId,
            AnonymousId = request.AnonymousId,
            GameId = gameId,
            Status = GamePlaySessionStatuses.Active,
            StartedAt = now,
            LastHeartbeatAt = now,
            AccumulatedActiveMs = 0,
            Source = AnalyticsSources.Normalize(request.Source),
            Platform = AnalyticsPlatforms.Normalize(request.Platform),
            DeviceType = string.IsNullOrWhiteSpace(request.DeviceType)
                ? null
                : request.DeviceType.Trim().ToUpperInvariant(),
            AppVersion = string.IsNullOrWhiteSpace(request.AppVersion) ? null : request.AppVersion.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await store.InsertAsync(entity, cancellationToken);

        await EmitAnalyticsAsync(
            AnalyticsEventTypes.GameStart,
            entity,
            new Dictionary<string, object?> { ["phase"] = "launch" },
            cancellationToken);

        await EmitAnalyticsAsync(
            AnalyticsEventTypes.GameSessionStart,
            entity,
            new Dictionary<string, object?> { ["status"] = entity.Status },
            cancellationToken);

        logger.LogInformation(
            "session_started sessionId={SessionId} gameId={GameId} source={Source}",
            entity.SessionId,
            entity.GameId,
            entity.Source);

        return ToResponse(entity);
    }

    public async Task<GamePlaySessionResponse> HeartbeatAsync(
        string sessionId,
        HeartbeatGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var entity = await LoadOwnedAsync(sessionId, authenticatedUserId, anonymousId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (GamePlaySessionStatuses.IsTerminal(entity.Status))
        {
            return ToResponse(entity);
        }

        if (IsPastMaxDuration(entity, now, opts))
        {
            return await FinalizeAsEndedAsync(
                entity,
                GamePlayEndReasons.MaxDuration,
                now,
                forceInvalidReason: GamePlaySessionInvalidReasons.SessionTooLong,
                cancellationToken);
        }

        if (!string.Equals(entity.Status, GamePlaySessionStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, GamePlaySessionStatuses.Started, StringComparison.OrdinalIgnoreCase))
        {
            // Heartbeats during PAUSED are ignored for active time but refresh ownership check.
            entity.UpdatedAt = now;
            await store.UpdateAsync(entity, cancellationToken);
            return ToResponse(entity);
        }

        if (entity.LastHeartbeatAt is not null)
        {
            var gapSeconds = (now - entity.LastHeartbeatAt.Value).TotalSeconds;
            if (gapSeconds > 0 && gapSeconds < 1)
            {
                // Excessive frequency — ignore credit but accept.
                entity.HeartbeatCount += 1;
                entity.UpdatedAt = now;
                await store.UpdateAsync(entity, cancellationToken);
                return ToResponse(entity);
            }

            if (gapSeconds > 0 && gapSeconds < opts.HeartbeatIntervalSeconds / 3.0 && entity.HeartbeatCount > 5)
            {
                logger.LogInformation(
                    "heartbeat_anomaly sessionId={SessionId} gapSeconds={Gap}",
                    entity.SessionId,
                    gapSeconds);
            }

            var credit = GamePlayActiveTimeCalculator.CreditHeartbeatMs(
                entity.LastHeartbeatAt.Value,
                now,
                opts.HeartbeatTimeoutSeconds,
                opts.MaxHeartbeatCreditSeconds);
            entity.AccumulatedActiveMs += credit;
        }

        entity.Status = GamePlaySessionStatuses.Active;
        entity.LastHeartbeatAt = now;
        entity.HeartbeatCount += 1;
        entity.ActiveSeconds = GamePlayActiveTimeCalculator.ToActiveSeconds(entity.AccumulatedActiveMs);
        entity.DurationSeconds = GamePlayActiveTimeCalculator.DurationSeconds(entity.StartedAt, now);
        entity.UpdatedAt = now;

        var shouldEmitHeartbeatAnalytics =
            entity.LastHeartbeatAnalyticsAt is null ||
            (now - entity.LastHeartbeatAnalyticsAt.Value).TotalSeconds >= opts.HeartbeatAnalyticsSampleSeconds;

        await store.UpdateAsync(entity, cancellationToken);

        if (shouldEmitHeartbeatAnalytics)
        {
            entity.LastHeartbeatAnalyticsAt = now;
            await store.UpdateAsync(entity, cancellationToken);
            await EmitAnalyticsAsync(
                AnalyticsEventTypes.GameSessionHeartbeat,
                entity,
                new Dictionary<string, object?>
                {
                    ["visibilityState"] = request.VisibilityState,
                    ["isFocused"] = request.IsFocused,
                    ["activeSeconds"] = entity.ActiveSeconds
                },
                cancellationToken);
        }

        logger.LogDebug(
            "heartbeat_received sessionId={SessionId} activeSeconds={ActiveSeconds}",
            entity.SessionId,
            entity.ActiveSeconds);

        return ToResponse(entity);
    }

    public async Task<GamePlaySessionResponse> PauseAsync(
        string sessionId,
        PauseGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var entity = await LoadOwnedAsync(sessionId, authenticatedUserId, anonymousId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (GamePlaySessionStatuses.IsTerminal(entity.Status))
        {
            return ToResponse(entity);
        }

        if (string.Equals(entity.Status, GamePlaySessionStatuses.Paused, StringComparison.OrdinalIgnoreCase))
        {
            return ToResponse(entity);
        }

        if (IsPastMaxDuration(entity, now, opts))
        {
            return await FinalizeAsEndedAsync(
                entity,
                GamePlayEndReasons.MaxDuration,
                now,
                forceInvalidReason: GamePlaySessionInvalidReasons.SessionTooLong,
                cancellationToken);
        }

        FlushActiveCredit(entity, now, opts);

        entity.Status = GamePlaySessionStatuses.Paused;
        entity.PausedAt = now;
        entity.PauseReason = GamePlayPauseReasons.Normalize(request.Reason);
        entity.ActiveSeconds = GamePlayActiveTimeCalculator.ToActiveSeconds(entity.AccumulatedActiveMs);
        entity.DurationSeconds = GamePlayActiveTimeCalculator.DurationSeconds(entity.StartedAt, now);
        entity.UpdatedAt = now;

        await store.UpdateAsync(entity, cancellationToken);
        await EmitAnalyticsAsync(
            AnalyticsEventTypes.GameSessionPause,
            entity,
            new Dictionary<string, object?> { ["reason"] = entity.PauseReason },
            cancellationToken);

        logger.LogInformation(
            "session_paused sessionId={SessionId} reason={Reason}",
            entity.SessionId,
            entity.PauseReason);

        return ToResponse(entity);
    }

    public async Task<GamePlaySessionResponse> ResumeAsync(
        string sessionId,
        ResumeGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var entity = await LoadOwnedAsync(sessionId, authenticatedUserId, anonymousId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (GamePlaySessionStatuses.IsTerminal(entity.Status))
        {
            throw new ValidationException(
                "Cannot resume an ended session.",
                new Dictionary<string, string[]>
                {
                    ["status"] = [GamePlaySessionInvalidReasons.InvalidStateTransition]
                });
        }

        if (IsPastMaxDuration(entity, now, opts))
        {
            return await FinalizeAsEndedAsync(
                entity,
                GamePlayEndReasons.MaxDuration,
                now,
                forceInvalidReason: GamePlaySessionInvalidReasons.SessionTooLong,
                cancellationToken);
        }

        if (string.Equals(entity.Status, GamePlaySessionStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return ToResponse(entity);
        }

        entity.Status = GamePlaySessionStatuses.Active;
        entity.ResumedAt = now;
        entity.LastHeartbeatAt = now;
        entity.UpdatedAt = now;
        entity.DurationSeconds = GamePlayActiveTimeCalculator.DurationSeconds(entity.StartedAt, now);

        await store.UpdateAsync(entity, cancellationToken);
        await EmitAnalyticsAsync(
            AnalyticsEventTypes.GameSessionResume,
            entity,
            new Dictionary<string, object?> { ["reason"] = request.Reason ?? "APP_FOREGROUND" },
            cancellationToken);

        logger.LogInformation("session_resumed sessionId={SessionId}", entity.SessionId);
        return ToResponse(entity);
    }

    public async Task<GamePlaySessionResponse> EndAsync(
        string sessionId,
        EndGamePlaySessionRequest request,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadOwnedAsync(sessionId, authenticatedUserId, anonymousId, cancellationToken);
        if (GamePlaySessionStatuses.IsTerminal(entity.Status))
        {
            return ToResponse(entity);
        }

        return await FinalizeAsEndedAsync(
            entity,
            GamePlayEndReasons.Normalize(request.Reason),
            DateTimeOffset.UtcNow,
            forceInvalidReason: null,
            cancellationToken);
    }

    public async Task<GamePlaySessionResponse> GetAsync(
        string sessionId,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadOwnedAsync(sessionId, authenticatedUserId, anonymousId, cancellationToken);
        return ToResponse(entity);
    }

    public Task<GamePlaySessionOverview> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        store.GetOverviewAsync(cancellationToken);

    private async Task<GamePlaySessionResponse> FinalizeAsEndedAsync(
        GamePlaySessionEntity entity,
        string endReason,
        DateTimeOffset now,
        string? forceInvalidReason,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (string.Equals(entity.Status, GamePlaySessionStatuses.Active, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entity.Status, GamePlaySessionStatuses.Started, StringComparison.OrdinalIgnoreCase))
        {
            FlushActiveCredit(entity, now, opts);
        }

        if (now < entity.StartedAt)
        {
            entity.Status = GamePlaySessionStatuses.Invalid;
            entity.IsValid = false;
            entity.InvalidReason = GamePlaySessionInvalidReasons.EndBeforeStart;
            entity.EndedAt = now;
            entity.EndReason = endReason;
            entity.UpdatedAt = now;
            await store.UpdateAsync(entity, cancellationToken);
            logger.LogInformation(
                "session_invalid sessionId={SessionId} reason={Reason}",
                entity.SessionId,
                entity.InvalidReason);
            return ToResponse(entity);
        }

        entity.EndedAt = now;
        entity.EndReason = endReason;
        entity.DurationSeconds = GamePlayActiveTimeCalculator.DurationSeconds(entity.StartedAt, now);
        entity.ActiveSeconds = GamePlayActiveTimeCalculator.ToActiveSeconds(entity.AccumulatedActiveMs);
        entity.UpdatedAt = now;

        if (forceInvalidReason is not null)
        {
            entity.Status = GamePlaySessionStatuses.Invalid;
            entity.IsValid = false;
            entity.InvalidReason = forceInvalidReason;
        }
        else
        {
            var (isValid, invalidReason) = GamePlayActiveTimeCalculator.EvaluateValidity(
                entity.ActiveSeconds,
                entity.DurationSeconds,
                opts.MinimumValidActiveSeconds,
                opts.MaximumSessionDurationSeconds);

            entity.IsValid = isValid;
            entity.InvalidReason = invalidReason;
            entity.Status = isValid ? GamePlaySessionStatuses.Ended : GamePlaySessionStatuses.Ended;
            // Keep ENDED even if invalid for XP eligibility — IsValid=false distinguishes.
            if (!isValid && invalidReason == GamePlaySessionInvalidReasons.SessionTooLong)
            {
                entity.Status = GamePlaySessionStatuses.Invalid;
            }
        }

        await store.UpdateAsync(entity, cancellationToken);

        await EmitAnalyticsAsync(
            AnalyticsEventTypes.GameSessionEnd,
            entity,
            new Dictionary<string, object?>
            {
                ["durationSeconds"] = entity.DurationSeconds,
                ["activeSeconds"] = entity.ActiveSeconds,
                ["isValid"] = entity.IsValid,
                ["invalidReason"] = entity.InvalidReason,
                ["endReason"] = entity.EndReason
            },
            cancellationToken);

        logger.LogInformation(
            "game_session_ended sessionId={SessionId} gameId={GameId} activeSeconds={ActiveSeconds} durationSeconds={DurationSeconds} isValid={IsValid}",
            entity.SessionId,
            entity.GameId,
            entity.ActiveSeconds,
            entity.DurationSeconds,
            entity.IsValid);

        return ToResponse(entity);
    }

    private async Task FinalizeAsInvalidAsync(
        GamePlaySessionEntity entity,
        string invalidReason,
        string endReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (GamePlaySessionStatuses.IsTerminal(entity.Status))
        {
            return;
        }

        FlushActiveCredit(entity, now, options.Value);
        entity.Status = GamePlaySessionStatuses.Invalid;
        entity.IsValid = false;
        entity.InvalidReason = invalidReason;
        entity.EndedAt = now;
        entity.EndReason = endReason;
        entity.DurationSeconds = GamePlayActiveTimeCalculator.DurationSeconds(entity.StartedAt, now);
        entity.ActiveSeconds = GamePlayActiveTimeCalculator.ToActiveSeconds(entity.AccumulatedActiveMs);
        entity.UpdatedAt = now;
        await store.UpdateAsync(entity, cancellationToken);

        logger.LogInformation(
            "session_invalid sessionId={SessionId} reason={Reason}",
            entity.SessionId,
            invalidReason);
    }

    private static void FlushActiveCredit(
        GamePlaySessionEntity entity,
        DateTimeOffset now,
        GameSessionOptions opts)
    {
        if (entity.LastHeartbeatAt is null)
        {
            return;
        }

        if (!string.Equals(entity.Status, GamePlaySessionStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, GamePlaySessionStatuses.Started, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var credit = GamePlayActiveTimeCalculator.CreditHeartbeatMs(
            entity.LastHeartbeatAt.Value,
            now,
            opts.HeartbeatTimeoutSeconds,
            opts.MaxHeartbeatCreditSeconds);
        entity.AccumulatedActiveMs += credit;
        entity.LastHeartbeatAt = now;
    }

    private static bool IsPastMaxDuration(
        GamePlaySessionEntity entity,
        DateTimeOffset now,
        GameSessionOptions opts) =>
        (now - entity.StartedAt).TotalSeconds > opts.MaximumSessionDurationSeconds;

    private async Task<GamePlaySessionEntity> LoadOwnedAsync(
        string sessionId,
        Guid? authenticatedUserId,
        Guid? anonymousId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ValidationException("sessionId is required.");
        }

        var entity = await store.GetBySessionIdAsync(sessionId.Trim(), cancellationToken)
                     ?? throw new NotFoundException("GamePlaySession", $"Session '{sessionId}' was not found.");

        EnsureOwnership(entity, authenticatedUserId, anonymousId);
        return entity;
    }

    private static void EnsureOwnership(
        GamePlaySessionEntity entity,
        Guid? authenticatedUserId,
        Guid? anonymousId)
    {
        if (authenticatedUserId is not null)
        {
            if (entity.UserId is not null && entity.UserId != authenticatedUserId)
            {
                throw new ForbiddenException("You do not own this game session.");
            }

            // Authenticated user may claim a session that started anonymous if anonymousId matches.
            if (entity.UserId is null &&
                entity.AnonymousId is not null &&
                anonymousId is not null &&
                entity.AnonymousId != anonymousId)
            {
                throw new ForbiddenException("You do not own this game session.");
            }

            return;
        }

        if (entity.AnonymousId is null || anonymousId is null || entity.AnonymousId != anonymousId)
        {
            throw new ForbiddenException("You do not own this game session.");
        }
    }

    private async Task EmitAnalyticsAsync(
        string eventType,
        GamePlaySessionEntity entity,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            await analytics.TrackAsync(
                new AnalyticsEventIngestRequest(
                    Guid.NewGuid(),
                    eventType,
                    entity.AnonymousId,
                    entity.SessionId,
                    entity.GameId,
                    entity.Source,
                    entity.Platform,
                    entity.DeviceType,
                    entity.AppVersion,
                    null,
                    null,
                    metadata,
                    DateTimeOffset.UtcNow),
                entity.UserId,
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to emit analytics event {EventType} for session {SessionId}", eventType, entity.SessionId);
        }
    }

    private static GamePlaySessionResponse ToResponse(GamePlaySessionEntity e) =>
        new(
            Guid.Parse(e.SessionId),
            e.GameId,
            e.Status,
            e.StartedAt,
            e.LastHeartbeatAt,
            e.PausedAt,
            e.ResumedAt,
            e.EndedAt,
            e.DurationSeconds,
            e.ActiveSeconds,
            e.IsValid,
            e.InvalidReason,
            e.Source,
            e.Platform);
}

using System.Text;
using System.Text.Json;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Domain;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Options;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Analytics.Services;

public interface IAnalyticsEventValidator
{
    AnalyticsEventWriteCommand ValidateAndNormalize(
        AnalyticsEventIngestRequest request,
        Guid? authenticatedUserId,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset receivedAt);
}

public sealed class AnalyticsEventValidator(IOptions<AnalyticsOptions> options) : IAnalyticsEventValidator
{
    public AnalyticsEventWriteCommand ValidateAndNormalize(
        AnalyticsEventIngestRequest request,
        Guid? authenticatedUserId,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset receivedAt)
    {
        var opts = options.Value;

        if (request.UserId is not null &&
            (authenticatedUserId is null || request.UserId != authenticatedUserId))
        {
            throw new ValidationException(
                "Client-supplied UserId is not allowed.",
                new Dictionary<string, string[]> { ["userId"] = ["UserId cannot be spoofed."] });
        }

        if (!AnalyticsEventTypes.TryNormalize(request.EventType, out var eventType))
        {
            throw new ValidationException(
                "Unsupported event type.",
                new Dictionary<string, string[]> { ["eventType"] = ["Unknown or unsupported eventType."] });
        }

        var eventId = request.EventId ?? Guid.NewGuid();
        if (eventId == Guid.Empty)
        {
            throw new ValidationException(
                "EventId is invalid.",
                new Dictionary<string, string[]> { ["eventId"] = ["EventId must be a non-empty UUID."] });
        }

        var metadata = request.Metadata ?? new Dictionary<string, object?>();
        var metadataJson = JsonSerializer.Serialize(metadata);
        var maxBytes = Math.Max(1, opts.MaxMetadataSizeKb) * 1024;
        if (Encoding.UTF8.GetByteCount(metadataJson) > maxBytes)
        {
            throw new ValidationException(
                "Metadata exceeds size limit.",
                new Dictionary<string, string[]>
                {
                    ["metadata"] = [$"Metadata must be <= {opts.MaxMetadataSizeKb} KB."]
                });
        }

        ValidateRequiredFields(eventType, request, metadata);

        if (authenticatedUserId is null)
        {
            if (!opts.EnableAnonymousTracking)
            {
                throw new ValidationException("Anonymous tracking is disabled.");
            }

            if (request.AnonymousId is null || request.AnonymousId == Guid.Empty)
            {
                throw new ValidationException(
                    "AnonymousId is required for unauthenticated events.",
                    new Dictionary<string, string[]> { ["anonymousId"] = ["AnonymousId is required."] });
            }
        }

        var occurredAt = request.OccurredAt?.ToUniversalTime() ?? receivedAt;
        if (occurredAt > receivedAt.AddMinutes(5))
        {
            occurredAt = receivedAt;
        }

        if (occurredAt < receivedAt.AddDays(-30))
        {
            throw new ValidationException(
                "OccurredAt is too old.",
                new Dictionary<string, string[]> { ["occurredAt"] = ["OccurredAt must be within the last 30 days."] });
        }

        return new AnalyticsEventWriteCommand(
            eventId,
            eventType,
            authenticatedUserId,
            request.AnonymousId,
            string.IsNullOrWhiteSpace(request.SessionId) ? null : request.SessionId.Trim(),
            request.GameId,
            AnalyticsSources.Normalize(request.Source),
            AnalyticsPlatforms.Normalize(request.Platform),
            string.IsNullOrWhiteSpace(request.DeviceType) ? null : request.DeviceType.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(request.AppVersion) ? null : request.AppVersion.Trim(),
            Truncate(request.PageUrl, 2048),
            Truncate(request.ReferrerUrl, 2048),
            metadata,
            HashIp(ipAddress, opts.IpHashSalt),
            Truncate(userAgent, 512),
            occurredAt,
            receivedAt);
    }

    private static void ValidateRequiredFields(
        string eventType,
        AnalyticsEventIngestRequest request,
        IReadOnlyDictionary<string, object?> metadata)
    {
        switch (eventType)
        {
            case AnalyticsEventTypes.GameView:
            case AnalyticsEventTypes.GameStart:
            case AnalyticsEventTypes.FavoriteAdded:
            case AnalyticsEventTypes.FavoriteRemoved:
            case AnalyticsEventTypes.RatingCreated:
            case AnalyticsEventTypes.ReviewCreated:
            case AnalyticsEventTypes.GameShared:
                RequireGameId(request.GameId, eventType);
                break;

            case AnalyticsEventTypes.GameSessionStart:
            case AnalyticsEventTypes.GameSessionHeartbeat:
            case AnalyticsEventTypes.GameSessionPause:
            case AnalyticsEventTypes.GameSessionResume:
            case AnalyticsEventTypes.GameSessionEnd:
                RequireGameId(request.GameId, eventType);
                if (string.IsNullOrWhiteSpace(request.SessionId))
                {
                    throw new ValidationException(
                        "sessionId is required.",
                        new Dictionary<string, string[]> { ["sessionId"] = [$"{eventType} requires sessionId."] });
                }

                break;

            case AnalyticsEventTypes.Search:
                if (!HasQuery(metadata))
                {
                    throw new ValidationException(
                        "search query is required.",
                        new Dictionary<string, string[]>
                        {
                            ["metadata.query"] = ["SEARCH events require metadata.query (or q)."]
                        });
                }

                break;
        }
    }

    private static void RequireGameId(Guid? gameId, string eventType)
    {
        if (gameId is null || gameId == Guid.Empty)
        {
            throw new ValidationException(
                "gameId is required.",
                new Dictionary<string, string[]> { ["gameId"] = [$"{eventType} requires gameId."] });
        }
    }

    private static bool HasQuery(IReadOnlyDictionary<string, object?> metadata)
    {
        foreach (var key in new[] { "query", "q", "searchQuery", "search" })
        {
            if (metadata.TryGetValue(key, out var value) &&
                value is not null &&
                !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string? HashIp(string? ipAddress, string salt)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var bytes = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes($"{salt}:{ipAddress.Trim()}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

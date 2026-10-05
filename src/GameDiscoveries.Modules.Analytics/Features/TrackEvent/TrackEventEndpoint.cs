using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Analytics.Features.TrackEvent;

/// <summary>
/// Legacy ingest endpoint kept for backward compatibility.
/// Prefer POST /api/v1/events and /api/v1/events/batch.
/// </summary>
public sealed record TrackEventRequest(
    string EventName,
    Guid? GameId,
    string? SessionId,
    Dictionary<string, object?>? Properties,
    Guid? EventId = null,
    Guid? AnonymousId = null,
    string? Source = null,
    string? Platform = null);

public static class TrackEventEndpoint
{
    public static IEndpointRouteBuilder MapTrackEvent(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/analytics/events", async (
                TrackEventRequest request,
                IAnalyticsEventService service,
                ICurrentUser currentUser,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.EventName))
                {
                    throw new ValidationException("EventName is required.");
                }

                Guid? userId = null;
                if (currentUser.IsAuthenticated && Guid.TryParse(currentUser.UserId, out var parsed))
                {
                    userId = parsed;
                }

                var result = await service.TrackAsync(
                    new AnalyticsEventIngestRequest(
                        request.EventId,
                        request.EventName,
                        request.AnonymousId,
                        request.SessionId,
                        request.GameId,
                        request.Source ?? "WEB",
                        request.Platform ?? "WEB",
                        null,
                        null,
                        null,
                        null,
                        request.Properties,
                        DateTimeOffset.UtcNow),
                    userId,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString(),
                    cancellationToken);

                return Results.Accepted(value: ApiResponse<object>.Ok(new
                {
                    tracked = result.Status is "accepted" or "duplicate",
                    eventId = result.EventId,
                    status = result.Status
                }));
            })
            .WithName("TrackAnalyticsEvent")
            .WithTags("Analytics")
            .WithSummary("Legacy analytics ingest endpoint.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}

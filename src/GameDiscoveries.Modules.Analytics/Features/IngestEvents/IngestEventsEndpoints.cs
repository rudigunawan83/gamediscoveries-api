using System.Diagnostics;
using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Models;
using GameDiscoveries.Modules.Analytics.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Analytics.Features.IngestEvents;

public static class IngestEventsEndpoints
{
    public static IEndpointRouteBuilder MapIngestEvents(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/events", async (
                AnalyticsEventIngestRequest request,
                IAnalyticsEventService service,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var sw = Stopwatch.StartNew();
                var userId = ResolveUserId(currentUser);
                var result = await service.TrackAsync(
                    request,
                    userId,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString(),
                    cancellationToken);

                loggerFactory.CreateLogger("Analytics.Ingest").LogInformation(
                    "analytics_ingest_completed status={Status} durationMs={DurationMs}",
                    result.Status,
                    sw.ElapsedMilliseconds);

                return Results.Accepted(
                    value: ApiResponse<object>.Ok(new
                    {
                        eventId = result.EventId,
                        status = result.Status,
                        accepted = result.Status is "accepted" or "duplicate" ? 1 : 0,
                        duplicates = result.Status == "duplicate" ? 1 : 0
                    }));
            })
            .WithName("IngestAnalyticsEvent")
            .WithTags("Analytics")
            .WithSummary("Ingest a single analytics event (idempotent by eventId).")
            .WithDescription(
                "Clients submit events only. UserId is resolved from JWT when present and cannot be spoofed. " +
                "Anonymous events require anonymousId. Duplicate eventId returns idempotent success.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/api/v1/events/batch", async (
                AnalyticsEventBatchRequest request,
                IAnalyticsEventService service,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var sw = Stopwatch.StartNew();
                var events = request.Events ?? Array.Empty<AnalyticsEventIngestRequest>();
                var userId = ResolveUserId(currentUser);
                var result = await service.TrackBatchAsync(
                    events,
                    userId,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString(),
                    cancellationToken);

                loggerFactory.CreateLogger("Analytics.Ingest").LogInformation(
                    "analytics_batch_completed accepted={Accepted} duplicates={Duplicates} rejected={Rejected} durationMs={DurationMs}",
                    result.Accepted,
                    result.Duplicates,
                    result.Rejected,
                    sw.ElapsedMilliseconds);

                return Results.Accepted(value: ApiResponse<AnalyticsBatchIngestResult>.Ok(result));
            })
            .WithName("IngestAnalyticsEventBatch")
            .WithTags("Analytics")
            .WithSummary("Ingest a batch of analytics events (mobile-friendly).")
            .WithDescription(
                "Validates each event independently. Duplicate eventIds are counted as duplicates (idempotent). " +
                "Invalid events are rejected without failing the whole batch. Max batch size is configurable.")
            .AllowAnonymous()
            .RequireRateLimiting("analytics")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapGet("/api/v1/admin/analytics/overview", async (
                IAnalyticsEventService service,
                CancellationToken cancellationToken) =>
            {
                var overview = await service.GetOverviewAsync(cancellationToken);
                return Results.Ok(ApiResponse<AnalyticsOverviewResponse>.Ok(overview));
            })
            .WithName("GetAnalyticsOverview")
            .WithTags("Administration", "Analytics")
            .WithSummary("Basic analytics overview for admins/moderators.")
            .RequireAuthorization(Policies.ModeratorOrAdmin)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static Guid? ResolveUserId(ICurrentUser currentUser)
    {
        if (currentUser.IsAuthenticated && Guid.TryParse(currentUser.UserId, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

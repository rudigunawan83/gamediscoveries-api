using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Analytics.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Analytics.Features.TrackEvent;

public sealed record TrackEventRequest(
    string EventName,
    Guid? GameId,
    string? SessionId,
    Dictionary<string, object?>? Properties);

public static class TrackEventEndpoint
{
    public static IEndpointRouteBuilder MapTrackEvent(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/analytics/events", async (
                TrackEventRequest request,
                IAnalyticsEventStore store,
                ICurrentUser currentUser,
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

                await store.TrackAsync(
                    new AnalyticsEventWriteModel(
                        request.EventName,
                        userId,
                        request.SessionId,
                        request.GameId,
                        request.Properties ?? new Dictionary<string, object?>()),
                    cancellationToken);

                return Results.Accepted(value: ApiResponse<object>.Ok(new { tracked = true }));
            })
            .WithName("TrackAnalyticsEvent")
            .WithTags("Analytics")
            .AllowAnonymous()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}

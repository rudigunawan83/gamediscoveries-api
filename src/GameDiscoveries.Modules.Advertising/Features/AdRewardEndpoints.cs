using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Advertising.Models;
using GameDiscoveries.Modules.Advertising.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Advertising.Features;

public static class AdRewardEndpoints
{
    public static IEndpointRouteBuilder MapAdRewardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/ad-rewards", async (
                IAdRewardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var status = await service.GetStatusAsync(RequireUser(currentUser), cancellationToken);
                return Results.Ok(ApiResponse<AdRewardStatusResponse>.Ok(status));
            })
            .WithName("GetMyAdRewards")
            .WithTags("AdRewards")
            .WithSummary("Rewarded video availability, XP per ad and today's remaining views.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/me/ad-rewards/tickets", async (
                CreateAdRewardTicketRequest? request,
                IAdRewardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var ticket = await service.CreateTicketAsync(
                    RequireUser(currentUser),
                    request?.Platform,
                    cancellationToken);
                return Results.Ok(ApiResponse<AdRewardTicketCreatedResponse>.Ok(ticket));
            })
            .WithName("CreateAdRewardTicket")
            .WithTags("AdRewards")
            .WithSummary("Issue a ticket to attach to a rewarded ad as SSV custom data. Grants no XP by itself.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/v1/me/ad-rewards/tickets/{ticketId:guid}", async (
                Guid ticketId,
                IAdRewardService service,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var ticket = await service.GetTicketAsync(RequireUser(currentUser), ticketId, cancellationToken);
                return Results.Ok(ApiResponse<AdRewardTicketResponse>.Ok(ticket));
            })
            .WithName("GetAdRewardTicket")
            .WithTags("AdRewards")
            .WithSummary("Poll whether AdMob has verified the view and XP was granted.")
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Called by Google, not by clients. Authenticity comes from the ECDSA signature.
        // Rate limiting is disabled because all callbacks arrive from a few Google IPs.
        endpoints.MapGet("/api/v1/ad-rewards/admob/ssv", async (
                HttpContext httpContext,
                IAdRewardService service,
                CancellationToken cancellationToken) =>
            {
                var outcome = await service.HandleAdMobCallbackAsync(
                    httpContext.Request.QueryString.Value ?? string.Empty,
                    cancellationToken);
                return outcome.SignatureValid ? Results.Ok() : Results.StatusCode(StatusCodes.Status403Forbidden);
            })
            .WithName("AdMobServerSideVerification")
            .WithTags("AdRewards")
            .WithSummary("AdMob rewarded ad server-side verification callback.")
            .AllowAnonymous()
            .DisableRateLimiting()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static Guid RequireUser(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        return userId;
    }
}

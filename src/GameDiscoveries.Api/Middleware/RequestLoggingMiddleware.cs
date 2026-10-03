using System.Diagnostics;
using GameDiscoveries.BuildingBlocks.Authentication;

namespace GameDiscoveries.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms TraceId {TraceId} UserId {UserId} Endpoint {Endpoint}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                traceId,
                currentUser.UserId ?? "anonymous",
                context.GetEndpoint()?.DisplayName ?? "unknown");
        }
    }
}

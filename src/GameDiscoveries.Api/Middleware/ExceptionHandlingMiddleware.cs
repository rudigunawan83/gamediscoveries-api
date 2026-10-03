using System.Diagnostics;
using FluentValidation;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Mvc;

namespace GameDiscoveries.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problem = exception switch
        {
            BuildingBlocks.Errors.ValidationException validation => CreateProblem(
                context,
                validation.Status,
                validation.Type,
                validation.Title,
                validation.Detail,
                traceId,
                validation.Errors),
            AppException appException => CreateProblem(
                context,
                appException.Status,
                appException.Type,
                appException.Title,
                appException.Detail,
                traceId),
            FluentValidation.ValidationException fluent => CreateProblem(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "https://api.gamediscoveries.com/errors/validation",
                "Validation Failed",
                "One or more validation errors occurred.",
                traceId,
                fluent.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            BadHttpRequestException badRequest => CreateProblem(
                context,
                StatusCodes.Status400BadRequest,
                "https://api.gamediscoveries.com/errors/bad-request",
                "Bad Request",
                badRequest.Message,
                traceId),
            _ => CreateProblem(
                context,
                StatusCodes.Status500InternalServerError,
                "https://api.gamediscoveries.com/errors/internal",
                "Internal Server Error",
                environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred.",
                traceId)
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception TraceId {TraceId} Path {Path}", traceId, context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Handled exception TraceId {TraceId} Status {Status} Path {Path}",
                traceId, problem.Status, context.Request.Path);
        }

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static ProblemDetails CreateProblem(
        HttpContext context,
        int status,
        string type,
        string title,
        string detail,
        string traceId,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = traceId;

        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }
}

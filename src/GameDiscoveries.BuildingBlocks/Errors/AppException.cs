namespace GameDiscoveries.BuildingBlocks.Errors;

public abstract class AppException : Exception
{
    protected AppException(
        string type,
        string title,
        int status,
        string detail)
        : base(detail)
    {
        Type = type;
        Title = title;
        Status = status;
        Detail = detail;
    }

    public string Type { get; }

    public string Title { get; }

    public int Status { get; }

    public string Detail { get; }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string title, string detail, string? type = null)
        : base(
            type ?? "https://api.gamediscoveries.com/errors/not-found",
            title,
            StatusCodes.Status404NotFound,
            detail)
    {
    }
}

public sealed class ValidationException : AppException
{
    public ValidationException(string detail, IDictionary<string, string[]>? errors = null)
        : base(
            "https://api.gamediscoveries.com/errors/validation",
            "Validation Failed",
            StatusCodes.Status422UnprocessableEntity,
            detail)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public IDictionary<string, string[]> Errors { get; }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string title, string detail)
        : base(
            "https://api.gamediscoveries.com/errors/conflict",
            title,
            StatusCodes.Status409Conflict,
            detail)
    {
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string detail)
        : base(
            "https://api.gamediscoveries.com/errors/forbidden",
            "Forbidden",
            StatusCodes.Status403Forbidden,
            detail)
    {
    }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string detail = "Authentication is required.")
        : base(
            "https://api.gamediscoveries.com/errors/unauthorized",
            "Unauthorized",
            StatusCodes.Status401Unauthorized,
            detail)
    {
    }
}

public sealed class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string detail = "Rate limit exceeded.")
        : base(
            "https://api.gamediscoveries.com/errors/rate-limit",
            "Too Many Requests",
            StatusCodes.Status429TooManyRequests,
            detail)
    {
    }
}

public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string detail)
        : base(
            "https://api.gamediscoveries.com/errors/service-unavailable",
            "Service Unavailable",
            StatusCodes.Status503ServiceUnavailable,
            detail)
    {
    }
}

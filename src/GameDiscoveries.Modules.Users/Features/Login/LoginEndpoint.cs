using FluentValidation;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Users.Features.Login;

public static class LoginEndpoint
{
    public static RouteHandlerBuilder MapLogin(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/api/v1/auth/login", async (
                LoginRequest request,
                LoginHandler handler,
                IValidator<LoginCommand> validator,
                CancellationToken cancellationToken) =>
            {
                var command = new LoginCommand(request.Email, request.Password);
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    var errors = validation.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray());

                    throw new BuildingBlocks.Errors.ValidationException(
                        "One or more validation errors occurred.",
                        errors);
                }

                var result = await handler.HandleAsync(command, cancellationToken);
                return Results.Ok(ApiResponse<LoginResponse>.Ok(result));
            })
            .WithName("Login")
            .WithTags("Authentication")
            .AllowAnonymous()
            .RequireRateLimiting("authentication")
            .Produces<ApiResponse<LoginResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

using FluentValidation;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Features.Login;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameDiscoveries.Modules.Users.Features.Register;

public static class RegisterEndpoint
{
    public static RouteHandlerBuilder MapRegister(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/api/v1/auth/register", async (
                RegisterRequest request,
                RegisterHandler handler,
                IValidator<RegisterCommand> validator,
                CancellationToken cancellationToken) =>
            {
                var command = new RegisterCommand(
                    request.Email,
                    request.Password,
                    request.DisplayName);

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
            .WithName("Register")
            .WithTags("Authentication")
            .AllowAnonymous()
            .RequireRateLimiting("authentication")
            .Produces<ApiResponse<LoginResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

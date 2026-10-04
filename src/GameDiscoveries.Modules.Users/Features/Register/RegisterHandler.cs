using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Features.Login;
using GameDiscoveries.Modules.Users.Models;
using GameDiscoveries.Modules.Users.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Users.Features.Register;

public sealed class RegisterHandler(
    IUserRepository users,
    IJwtTokenService tokenService,
    ILogger<RegisterHandler> logger)
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public async Task<LoginResponse> HandleAsync(
        RegisterCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = command.Email.Trim();
        var displayName = command.DisplayName.Trim();

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException(
                "Email already registered",
                "An account with this email already exists.");
        }

        var userId = Guid.NewGuid();
        var passwordHash = _passwordHasher.HashPassword(new object(), command.Password);

        await users.CreateUserAsync(
            userId,
            email,
            passwordHash,
            displayName,
            ["Player"],
            cancellationToken);

        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceUnavailableException(
                "Account was created but could not be loaded.");

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(user);

        logger.LogInformation("User registered userId={UserId}", user.Id);

        return new LoginResponse(
            AccessToken: accessToken,
            ExpiresIn: expiresIn,
            User: UserResponseMapper.From(user));
    }
}

using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Models;
using GameDiscoveries.Modules.Users.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace GameDiscoveries.Modules.Users.Features.Login;

public sealed class LoginHandler(
    IUserRepository users,
    IJwtTokenService tokenService,
    ILogger<LoginHandler> logger)
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public async Task<LoginResponse> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await users.GetByEmailWithPasswordAsync(command.Email.Trim(), cancellationToken);
        if (account is null)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var user = account.Value.User;
        var passwordHash = account.Value.PasswordHash;
        if (!string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException("This account is not active.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            new object(),
            passwordHash,
            command.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(user);

        logger.LogInformation("User logged in userId={UserId}", user.Id);

        return new LoginResponse(
            AccessToken: accessToken,
            ExpiresIn: expiresIn,
            User: UserResponseMapper.From(user));
    }
}

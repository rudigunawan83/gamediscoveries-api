using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Authorization;
using GameDiscoveries.Modules.Users.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Users.Services;

/// <summary>
/// Creates an optional bootstrap user when Authentication:SeedEmail/SeedPassword are set
/// and the users table has no rows yet.
/// </summary>
public sealed class AuthSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuthenticationOptions> options,
    ILogger<AuthSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var auth = options.Value;
        if (string.IsNullOrWhiteSpace(auth.SeedEmail) || string.IsNullOrWhiteSpace(auth.SeedPassword))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        if (await users.AnyUsersAsync(cancellationToken))
        {
            return;
        }

        var hasher = new PasswordHasher<object>();
        var hash = hasher.HashPassword(new object(), auth.SeedPassword);
        var id = Guid.NewGuid();

        await users.CreateUserAsync(
            id,
            auth.SeedEmail.Trim(),
            hash,
            string.IsNullOrWhiteSpace(auth.SeedDisplayName) ? "Admin" : auth.SeedDisplayName.Trim(),
            [Roles.Player, Roles.Admin],
            cancellationToken);

        logger.LogInformation("Seeded bootstrap auth user for {Email}", auth.SeedEmail);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

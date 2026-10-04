using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.Modules.Users.Features.GetCurrentUser;

public sealed class GetCurrentUserHandler(
    ICurrentUser currentUser,
    IUserRepository users)
{
    public async Task<UserResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new UnauthorizedException();
        }

        if (!Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException();
        }

        return UserResponseMapper.From(user);
    }
}

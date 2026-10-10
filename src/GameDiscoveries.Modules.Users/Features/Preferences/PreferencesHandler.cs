using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.Modules.Users.Features.Preferences;

public sealed record UpdatePreferencesRequest(string? PreferredLanguage);

public sealed class PreferencesHandler(ICurrentUser currentUser, IUserRepository users)
{
    public async Task<UserResponse> UpdateAsync(
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireActiveUserAsync(cancellationToken);

        if (!PreferredLanguage.TryNormalize(request.PreferredLanguage, out var language))
        {
            throw new ValidationException(
                "Preferred language is not supported.",
                new Dictionary<string, string[]>
                {
                    ["preferredLanguage"] = [$"Allowed values: {string.Join(", ", PreferredLanguage.All)}."]
                });
        }

        await users.UpdatePreferredLanguageAsync(userId, language, cancellationToken);

        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new UnauthorizedException();
        return UserResponseMapper.From(user);
    }

    private async Task<Guid> RequireActiveUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId))
        {
            throw new UnauthorizedException();
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException();
        }

        return userId;
    }
}

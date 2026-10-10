using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.Modules.Users.Features.Profile;

public sealed record UpdateProfileRequest(string? DisplayName, string? Username);

public sealed class ProfileHandler(ICurrentUser currentUser, IUserRepository users)
{
    public async Task<UserResponse> UpdateAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await RequireActiveUserAsync(cancellationToken);
        var displayName = request.DisplayName?.Trim() ?? "";
        var username = request.Username?.Trim().ToLowerInvariant() ?? "";

        var nameUnchanged = string.Equals(displayName, account.DisplayName?.Trim(), StringComparison.Ordinal);
        var handleUnchanged = string.Equals(username, account.Username, StringComparison.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string[]>();

        if (!nameUnchanged && !ProfileRules.IsValidDisplayName(displayName))
        {
            errors["displayName"] =
            [
                $"Use {ProfileRules.DisplayNameMinLength}–{ProfileRules.DisplayNameMaxLength} characters."
            ];
        }

        if (!handleUnchanged && !ProfileRules.IsValidUsername(username))
        {
            errors["username"] =
            [
                $"Use {ProfileRules.UsernameMinLength}–{ProfileRules.UsernameMaxLength} letters, numbers, or hyphens."
            ];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Display name or username is invalid.", errors);
        }

        if (nameUnchanged && handleUnchanged)
        {
            return UserResponseMapper.From(account);
        }

        var status = await users.UpdateProfileAsync(account.Id, displayName, username, cancellationToken);
        switch (status)
        {
            case ProfileUpdateStatus.UsernameTaken:
                throw new ConflictException("Username taken", "That username is already taken.");
            case ProfileUpdateStatus.Missing:
                throw new UnauthorizedException();
        }

        var updated = await users.GetByIdAsync(account.Id, cancellationToken) ?? throw new UnauthorizedException();
        return UserResponseMapper.From(updated);
    }

    private async Task<UserAccount> RequireActiveUserAsync(CancellationToken cancellationToken)
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

        return user;
    }
}

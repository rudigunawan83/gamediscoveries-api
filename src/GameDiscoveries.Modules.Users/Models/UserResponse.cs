namespace GameDiscoveries.Modules.Users.Models;

public sealed record UserResponse(
    string Id,
    string Email,
    string? DisplayName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    string? Username,
    string PreferredLanguage);

public static class UserResponseMapper
{
    public static UserResponse From(UserAccount user) =>
        new(
            user.Id.ToString(),
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.Roles,
            user.Username,
            user.PreferredLanguage);
}

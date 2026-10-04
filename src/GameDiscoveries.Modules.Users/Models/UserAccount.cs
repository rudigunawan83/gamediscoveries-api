namespace GameDiscoveries.Modules.Users.Models;

public sealed class UserAccount
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public required string Status { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }
}

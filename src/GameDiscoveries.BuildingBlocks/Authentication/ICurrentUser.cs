namespace GameDiscoveries.BuildingBlocks.Authentication;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }
}

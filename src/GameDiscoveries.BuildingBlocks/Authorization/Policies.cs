namespace GameDiscoveries.BuildingBlocks.Authorization;

public static class Policies
{
    public const string Authenticated = "Authenticated";
    public const string AdminOnly = "AdminOnly";
    public const string DeveloperOnly = "DeveloperOnly";
    public const string ModeratorOrAdmin = "ModeratorOrAdmin";
}

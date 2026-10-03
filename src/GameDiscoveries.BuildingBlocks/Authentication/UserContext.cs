namespace GameDiscoveries.BuildingBlocks.Authentication;

public sealed class UserContext(ICurrentUser currentUser)
{
    public ICurrentUser CurrentUser { get; } = currentUser;

    public string RequireUserId()
    {
        if (!CurrentUser.IsAuthenticated || string.IsNullOrWhiteSpace(CurrentUser.UserId))
        {
            throw new Errors.UnauthorizedException();
        }

        return CurrentUser.UserId;
    }
}

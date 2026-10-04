using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.Modules.Users.Services;

public interface IJwtTokenService
{
    (string AccessToken, int ExpiresInSeconds) CreateAccessToken(UserAccount user);
}

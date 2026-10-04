using GameDiscoveries.Modules.Users.Models;

namespace GameDiscoveries.Modules.Users.Features.Login;

public sealed record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    UserResponse User,
    string? RefreshToken = null);

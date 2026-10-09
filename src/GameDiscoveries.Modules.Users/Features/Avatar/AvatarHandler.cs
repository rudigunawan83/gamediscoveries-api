using GameDiscoveries.BuildingBlocks.Authentication;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Users.Features.Avatar;

public sealed class AvatarHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    AvatarStorage storage,
    IOptions<AvatarOptions> options)
{
    public async Task<UserResponse> UploadAsync(
        IFormFile? file,
        string requestOrigin,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireActiveUserAsync(cancellationToken);
        var settings = options.Value;

        if (file is null || file.Length == 0)
        {
            throw new ValidationException("Choose an image to upload.");
        }

        if (file.Length > settings.MaxUploadBytes)
        {
            throw new ValidationException($"Image must be {settings.MaxUploadBytes / (1024 * 1024)} MB or smaller.");
        }

        byte[] jpeg;
        await using (var stream = file.OpenReadStream())
        {
            jpeg = await AvatarImageProcessor.NormalizeAsync(stream, settings.OutputSize, cancellationToken);
        }

        var relativeUrl = await storage.SaveAsync(userId, jpeg, cancellationToken);
        var origin = string.IsNullOrWhiteSpace(settings.PublicBaseUrl) ? requestOrigin : settings.PublicBaseUrl;
        var previous = await users.UpdateAvatarUrlAsync(userId, origin.TrimEnd('/') + relativeUrl, cancellationToken);
        storage.DeleteOwned(userId, previous);

        return await CurrentAsync(userId, cancellationToken);
    }

    public async Task<UserResponse> RemoveAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireActiveUserAsync(cancellationToken);
        var previous = await users.UpdateAvatarUrlAsync(userId, null, cancellationToken);
        storage.DeleteOwned(userId, previous);
        return await CurrentAsync(userId, cancellationToken);
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

    private async Task<UserResponse> CurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new UnauthorizedException();
        return UserResponseMapper.From(user);
    }
}

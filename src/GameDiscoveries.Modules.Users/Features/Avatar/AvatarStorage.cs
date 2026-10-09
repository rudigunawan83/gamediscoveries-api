using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Users.Features.Avatar;

/// <summary>
/// Stores avatars on local disk as <c>{root}/avatars/{userId:N}/{random:N}.jpg</c>.
/// File names are random so URLs can be cached forever.
/// </summary>
public sealed partial class AvatarStorage(IOptions<AvatarOptions> options, IHostEnvironment environment)
{
    public const string RoutePrefix = "/media/avatars";

    private readonly string _root = Path.Combine(
        Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath),
        "avatars");

    public async Task<string> SaveAsync(Guid userId, byte[] jpeg, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_root, userId.ToString("N"));
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}.jpg";
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), jpeg, cancellationToken);
        return $"{RoutePrefix}/{userId:N}/{fileName}";
    }

    public string? TryGetFilePath(Guid userId, string fileName)
    {
        if (!FileNamePattern().IsMatch(fileName))
        {
            return null;
        }

        var path = Path.Combine(_root, userId.ToString("N"), fileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Deletes the file behind <paramref name="avatarUrl"/> only if it is this user's stored avatar.</summary>
    public void DeleteOwned(Guid userId, string? avatarUrl)
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            return;
        }

        var match = StoredUrlPattern().Match(avatarUrl);
        if (!match.Success || match.Groups["user"].Value != userId.ToString("N"))
        {
            return;
        }

        var path = TryGetFilePath(userId, match.Groups["file"].Value);
        if (path is not null)
        {
            File.Delete(path);
        }
    }

    [GeneratedRegex("^[a-f0-9]{32}\\.jpg$")]
    private static partial Regex FileNamePattern();

    [GeneratedRegex("/media/avatars/(?<user>[a-f0-9]{32})/(?<file>[a-f0-9]{32}\\.jpg)$")]
    private static partial Regex StoredUrlPattern();
}

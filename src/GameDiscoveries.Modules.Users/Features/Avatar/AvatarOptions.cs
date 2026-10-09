namespace GameDiscoveries.Modules.Users.Features.Avatar;

public sealed class AvatarOptions
{
    public const string SectionName = "Avatars";

    /// <summary>Directory for stored avatars; relative paths resolve against the content root.</summary>
    public string RootPath { get; set; } = "media";

    /// <summary>Origin used in stored avatar URLs. Falls back to the request origin when unset.</summary>
    public string? PublicBaseUrl { get; set; }

    public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024;

    public int OutputSize { get; set; } = 512;
}

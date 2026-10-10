namespace GameDiscoveries.Api.Options;

/// <summary>
/// Mobile app release info. Builds are the integer build numbers
/// (Android versionCode / iOS CFBundleVersion).
/// </summary>
public sealed class AppVersionOptions
{
    public const string SectionName = "AppVersion";

    public AppPlatformVersionOptions Android { get; set; } = new();

    public AppPlatformVersionOptions Ios { get; set; } = new();
}

public sealed class AppPlatformVersionOptions
{
    public string LatestVersion { get; set; } = "1.0.0";

    public int LatestBuild { get; set; } = 1;

    /// <summary>Builds below this must update before the app can be used.</summary>
    public int MinSupportedBuild { get; set; } = 1;

    public string? StoreUrl { get; set; }

    /// <summary>Direct download for installs outside the store (Android only).</summary>
    public string? ApkUrl { get; set; }

    public string? ReleaseNotes { get; set; }
}

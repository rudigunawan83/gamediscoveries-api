using GameDiscoveries.Api.Options;
using GameDiscoveries.BuildingBlocks.Errors;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Api.Extensions;

public sealed record AppVersionResponse(
    string Platform,
    string LatestVersion,
    int LatestBuild,
    int MinSupportedBuild,
    string? StoreUrl,
    string? ApkUrl,
    string? ReleaseNotes)
{
    public static AppVersionResponse For(string? platform, AppVersionOptions options)
    {
        var (name, release) = platform?.Trim().ToLowerInvariant() switch
        {
            "android" => ("android", options.Android),
            "ios" => ("ios", options.Ios),
            _ => throw new ValidationException(
                "platform must be 'android' or 'ios'.",
                new Dictionary<string, string[]> { ["platform"] = ["Use 'android' or 'ios'."] })
        };

        return new AppVersionResponse(
            name,
            release.LatestVersion,
            release.LatestBuild,
            Math.Min(release.MinSupportedBuild, release.LatestBuild),
            Blank(release.StoreUrl),
            name == "android" ? Blank(release.ApkUrl) : null,
            Blank(release.ReleaseNotes));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class AppVersionEndpoints
{
    public static WebApplication MapAppVersionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/app/version", (string? platform, IOptionsMonitor<AppVersionOptions> options) =>
                Results.Ok(ApiResponse<AppVersionResponse>.Ok(AppVersionResponse.For(platform, options.CurrentValue))))
            .WithName("GetAppVersion")
            .WithTags("App")
            .WithSummary("Latest and minimum supported mobile app build for a platform.")
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}

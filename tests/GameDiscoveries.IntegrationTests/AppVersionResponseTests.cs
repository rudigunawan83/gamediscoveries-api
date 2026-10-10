using FluentAssertions;
using GameDiscoveries.Api.Extensions;
using GameDiscoveries.Api.Options;
using GameDiscoveries.BuildingBlocks.Errors;

namespace GameDiscoveries.IntegrationTests;

public sealed class AppVersionResponseTests
{
    private static AppVersionOptions Options() => new()
    {
        Android = new AppPlatformVersionOptions
        {
            LatestVersion = "1.2.0",
            LatestBuild = 12,
            MinSupportedBuild = 10,
            StoreUrl = "https://play.google.com/store/apps/details?id=x",
            ApkUrl = "https://gamediscoveries.com/downloads/gamediscoveries.apk",
            ReleaseNotes = " "
        },
        Ios = new AppPlatformVersionOptions
        {
            LatestBuild = 3,
            MinSupportedBuild = 9,
            ApkUrl = "https://should-not-leak.example/app.apk"
        }
    };

    [Fact]
    public void Android_returns_configured_release()
    {
        var response = AppVersionResponse.For("Android", Options());

        response.Platform.Should().Be("android");
        response.LatestBuild.Should().Be(12);
        response.MinSupportedBuild.Should().Be(10);
        response.ApkUrl.Should().Be("https://gamediscoveries.com/downloads/gamediscoveries.apk");
        response.ReleaseNotes.Should().BeNull();
    }

    [Fact]
    public void Ios_never_exposes_an_apk_and_caps_minimum_at_latest()
    {
        var response = AppVersionResponse.For("ios", Options());

        response.ApkUrl.Should().BeNull();
        response.MinSupportedBuild.Should().Be(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("web")]
    public void Unknown_platform_is_rejected(string? platform)
    {
        var act = () => AppVersionResponse.For(platform, Options());

        act.Should().Throw<ValidationException>();
    }
}

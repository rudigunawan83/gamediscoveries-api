using FluentAssertions;
using GameDiscoveries.Modules.Community.Services;

namespace GameDiscoveries.UnitTests.Community;

public sealed class ContentSanitizerTests
{
    [Fact]
    public void Strips_Html_And_Trims()
    {
        ContentSanitizer.SanitizePlainText("  <b>Hello</b> world  ", 100)
            .Should().Be("Hello world");
    }

    [Fact]
    public void Slugifies_Title()
    {
        ContentSanitizer.ToSlug("Is this game worth playing?")
            .Should().Be("is-this-game-worth-playing");
    }

    [Fact]
    public void Username_Is_Normalized()
    {
        ContentSanitizer.ToUsername("Demo Player!")
            .Should().Be("demo-player");
    }
}

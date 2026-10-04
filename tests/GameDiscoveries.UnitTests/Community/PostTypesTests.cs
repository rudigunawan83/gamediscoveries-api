using FluentAssertions;
using GameDiscoveries.Modules.Community.Domain;

namespace GameDiscoveries.UnitTests.Community;

public sealed class PostTypesTests
{
    [Theory]
    [InlineData("discussion")]
    [InlineData("Discussion")]
    [InlineData("game_share")]
    [InlineData("recommendation")]
    [InlineData("question")]
    [InlineData("achievement_share")]
    public void Accepts_Known_Post_Types(string type)
    {
        PostTypes.All.Contains(type).Should().BeTrue();
    }

    [Fact]
    public void Rejects_Unknown_Post_Type()
    {
        PostTypes.All.Contains("status_update").Should().BeFalse();
    }

    [Theory]
    [InlineData("like")]
    [InlineData("helpful")]
    [InlineData("love")]
    [InlineData("funny")]
    public void Accepts_Known_Reactions(string reaction)
    {
        ReactionKinds.All.Contains(reaction).Should().BeTrue();
    }
}

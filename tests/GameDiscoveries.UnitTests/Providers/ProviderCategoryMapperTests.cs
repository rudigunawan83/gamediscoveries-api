using FluentAssertions;
using GameDiscoveries.Infrastructure.Providers.Normalization;

namespace GameDiscoveries.UnitTests.Providers;

public sealed class ProviderCategoryMapperTests
{
    [Theory]
    [InlineData("2 Player", "2 Player")]
    [InlineData("Puzzles", "Puzzle")]
    [InlineData("Clicker", "Hypercasual")]
    [InlineData("Custom Genre", "Custom Genre")]
    public void Maps_known_and_passthrough_categories(string input, string expected)
    {
        ProviderCategoryMapper.MapGameMonetize(input).Should().Be(expected);
    }
}

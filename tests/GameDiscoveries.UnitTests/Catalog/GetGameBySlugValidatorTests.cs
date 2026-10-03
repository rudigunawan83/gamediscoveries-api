using FluentAssertions;
using GameDiscoveries.Modules.Catalog.Features.GetGameBySlug;

namespace GameDiscoveries.UnitTests.Catalog;

public sealed class GetGameBySlugValidatorTests
{
    private readonly GetGameBySlugValidator _validator = new();

    [Theory]
    [InlineData("example-game")]
    [InlineData("racing-car-2")]
    [InlineData("a")]
    public async Task Valid_slug_passes(string slug)
    {
        var result = await _validator.ValidateAsync(new GetGameBySlugQuery(slug));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Example-Game")]
    [InlineData("example_game")]
    [InlineData("example game")]
    [InlineData("-example")]
    [InlineData("example-")]
    public async Task Invalid_slug_fails(string slug)
    {
        var result = await _validator.ValidateAsync(new GetGameBySlugQuery(slug));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetGameBySlugQuery.Slug));
    }
}

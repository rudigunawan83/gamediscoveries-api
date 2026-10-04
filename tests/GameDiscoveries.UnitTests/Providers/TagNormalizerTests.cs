using FluentAssertions;
using GameDiscoveries.Infrastructure.Providers.Normalization;

namespace GameDiscoveries.UnitTests.Providers;

public sealed class TagNormalizerTests
{
    [Fact]
    public void Normalizes_trim_dedupe_and_separators()
    {
        var tags = TagNormalizer.Normalize([" Action ", "action", "cars/arcade", "  ", "ACTION"]);

        // First occurrence casing preserved; case-insensitive duplicates removed; separators normalized.
        tags.Should().Equal("Action", "cars arcade");
    }

    [Fact]
    public void NormalizeKey_is_lowercase()
    {
        TagNormalizer.NormalizeKey("  Racing ").Should().Be("racing");
    }
}

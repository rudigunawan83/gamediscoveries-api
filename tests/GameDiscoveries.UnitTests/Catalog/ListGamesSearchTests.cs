using FluentAssertions;
using GameDiscoveries.Modules.Catalog.Features.ListGames;

namespace GameDiscoveries.UnitTests.Catalog;

public sealed class ListGamesSearchTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" relevance ", true)]
    [InlineData("newest", false)]
    [InlineData("popular", false)]
    [InlineData("title", false)]
    public void Search_ranks_by_relevance_only_without_an_explicit_sort(string? sort, bool expected)
    {
        ListGamesHandler.IsRelevanceSort(sort).Should().Be(expected);
    }
}

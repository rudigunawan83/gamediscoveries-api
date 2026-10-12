using FluentAssertions;
using GameDiscoveries.Modules.Administration.Features.Users;

namespace GameDiscoveries.UnitTests.Administration;

public sealed class AdminUsersQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_normalizes_known_values()
    {
        var filter = AdminUsersQuery.Parse("  rudi ", "SUSPENDED", "moderator", "30d", "games", "asc", Now);

        filter.Search.Should().Be("rudi");
        filter.Status.Should().Be("suspended");
        filter.Role.Should().Be("Moderator");
        filter.JoinedFrom.Should().Be(Now.AddDays(-30));
        filter.SortColumn.Should().Be("games_played");
        filter.Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_ignores_unknown_values_instead_of_passing_them_to_sql()
    {
        var filter = AdminUsersQuery.Parse(" ", "pending", "root", "forever", "email; DROP TABLE users", null, Now);

        filter.Search.Should().BeNull();
        filter.Status.Should().BeNull();
        filter.Role.Should().BeNull();
        filter.JoinedFrom.Should().BeNull();
        filter.SortColumn.Should().Be("created_at");
        filter.Descending.Should().BeTrue();
    }

    [Fact]
    public void Cumulative_trend_ends_at_the_current_total()
    {
        // Newest first: 2 joined in the last 24 hours, 1 the day before, 3 the day before that.
        AdminUsersQuery.CumulativeTrend(10, [2, 1, 3]).Should().Equal(7, 8, 10);
    }

    [Fact]
    public void Change_vs_average_needs_a_baseline()
    {
        AdminUsersQuery.ChangeVsAverage(12, [10, 10, 10]).Should().Be(20);
        AdminUsersQuery.ChangeVsAverage(5, [0, 0]).Should().BeNull();
        AdminUsersQuery.ChangeVsAverage(5, []).Should().BeNull();
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Alex Morgan", "Alex Morgan")]
    [InlineData("Morgan, Alex", "\"Morgan, Alex\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+62812", "'+62812")]
    public void Csv_cells_are_quoted_and_formula_safe(string? value, string expected)
    {
        AdminUsersQuery.CsvCell(value).Should().Be(expected);
    }
}

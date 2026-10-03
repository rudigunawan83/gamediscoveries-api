using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.BuildingBlocks.Pagination;

namespace GameDiscoveries.UnitTests.Errors;

public sealed class ApiResponseTests
{
    [Fact]
    public void Ok_wraps_data_without_error()
    {
        var response = ApiResponse<string>.Ok("hello");

        response.Success.Should().BeTrue();
        response.Data.Should().Be("hello");
        response.Error.Should().BeNull();
        response.Meta.Should().BeNull();
    }

    [Fact]
    public void Ok_includes_pagination_meta()
    {
        var meta = PaginationMeta.Create(1, 20, 100);
        var response = ApiResponse<int[]>.Ok([1, 2], meta);

        response.Meta.Should().NotBeNull();
        response.Meta!.Total.Should().Be(100);
        response.Meta.TotalPages.Should().Be(5);
    }
}

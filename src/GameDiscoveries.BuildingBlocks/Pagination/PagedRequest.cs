namespace GameDiscoveries.BuildingBlocks.Pagination;

public abstract record PagedRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int Skip => Math.Max(0, (Math.Max(Page, 1) - 1) * Math.Clamp(PageSize, 1, 100));

    public int Take => Math.Clamp(PageSize, 1, 100);
}

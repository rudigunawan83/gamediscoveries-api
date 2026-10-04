namespace GameDiscoveries.BuildingBlocks.Pagination;

public abstract record PagedRequest
{
    public const int MaxPageSize = 500;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int Skip => Math.Max(0, (Math.Max(Page, 1) - 1) * Math.Clamp(PageSize, 1, MaxPageSize));

    public int Take => Math.Clamp(PageSize, 1, MaxPageSize);
}

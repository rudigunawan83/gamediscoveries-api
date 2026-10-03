namespace GameDiscoveries.BuildingBlocks.Pagination;

public sealed record PaginationMeta(
    int Page,
    int PageSize,
    long Total,
    int TotalPages)
{
    public static PaginationMeta Create(int page, int pageSize, long total)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);
        var totalPages = total <= 0 ? 0 : (int)Math.Ceiling(total / (double)safePageSize);

        return new PaginationMeta(safePage, safePageSize, total, totalPages);
    }
}

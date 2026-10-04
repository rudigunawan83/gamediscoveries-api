namespace GameDiscoveries.Modules.Catalog.Features.ListCategories;

public sealed record CategoryResponse(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    int GameCount,
    DateTimeOffset? LastContentAt);

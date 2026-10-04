namespace GameDiscoveries.Infrastructure.Providers.Search;

public sealed class GameSearchDocument
{
    public required string Id { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Category { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Developer { get; init; }

    public string Provider { get; init; } = "GameMonetize";

    public string Status { get; init; } = "published";

    public bool MobileReady { get; init; }
}

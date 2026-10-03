namespace GameDiscoveries.Infrastructure.Providers.Abstractions;

public sealed class ExternalGame
{
    public required string ProviderGameId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? CoverUrl { get; init; }

    public string? GameUrl { get; init; }

    public string? ProviderUrl { get; init; }

    public bool MobileReady { get; init; }

    public string? Orientation { get; init; }

    public IReadOnlyCollection<string> Categories { get; init; } = [];

    public IReadOnlyCollection<string> Tags { get; init; } = [];

    public string? RawPayload { get; init; }
}

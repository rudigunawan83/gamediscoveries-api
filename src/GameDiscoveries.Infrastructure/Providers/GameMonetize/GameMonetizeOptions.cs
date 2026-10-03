using System.ComponentModel.DataAnnotations;

namespace GameDiscoveries.Infrastructure.Providers.GameMonetize;

public sealed class GameMonetizeOptions
{
    public const string SectionName = "GameMonetize";

    public bool Enabled { get; set; }

    [Url]
    public string? FeedUrl { get; set; }

    public string? ApiKey { get; set; }
}

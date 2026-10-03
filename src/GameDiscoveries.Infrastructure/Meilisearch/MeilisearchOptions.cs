using System.ComponentModel.DataAnnotations;

namespace GameDiscoveries.Infrastructure.Meilisearch;

public sealed class MeilisearchOptions
{
    public const string SectionName = "Meilisearch";

    [Required]
    public string Url { get; set; } = "http://localhost:7700";

    public string? ApiKey { get; set; }

    public bool Enabled { get; set; } = true;
}

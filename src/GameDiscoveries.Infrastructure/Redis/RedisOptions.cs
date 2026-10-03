using System.ComponentModel.DataAnnotations;

namespace GameDiscoveries.Infrastructure.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string ConnectionString { get; set; } = "localhost:6379";

    public string InstanceName { get; set; } = "gamediscoveries:";

    public bool Enabled { get; set; } = true;
}

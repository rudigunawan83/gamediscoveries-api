using System.ComponentModel.DataAnnotations;

namespace GameDiscoveries.Infrastructure.PostgreSQL;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public bool ApplyMigrationsOnStartup { get; set; } = true;
}

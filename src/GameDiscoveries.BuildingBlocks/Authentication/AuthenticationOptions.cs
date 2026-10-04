namespace GameDiscoveries.BuildingBlocks.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// External OIDC authority. When set, JWT validation uses Authority metadata.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = "gamediscoveries-api";

    public string Issuer { get; set; } = "gamediscoveries-api";

    /// <summary>
    /// Symmetric signing key for local email/password JWT issuance and validation.
    /// Required when Authority is empty and local login is used.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public bool RequireHttpsMetadata { get; set; } = true;

    public bool Enabled { get; set; }

    /// <summary>
    /// Optional bootstrap account created when the users table is empty.
    /// </summary>
    public string? SeedEmail { get; set; }

    public string? SeedPassword { get; set; }

    public string? SeedDisplayName { get; set; }
}

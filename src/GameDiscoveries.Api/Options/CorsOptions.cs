namespace GameDiscoveries.Api.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; set; } = true;

    public string ServiceName { get; set; } = "GameDiscoveries.Api";

    public string? OtlpEndpoint { get; set; }

    public bool PrometheusEnabled { get; set; } = true;
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PublicPermitLimit { get; set; } = 600;

    public int SearchPermitLimit { get; set; } = 30;

    public int AuthenticationPermitLimit { get; set; } = 10;

    public int AnalyticsPermitLimit { get; set; } = 120;

    public int WindowSeconds { get; set; } = 60;
}

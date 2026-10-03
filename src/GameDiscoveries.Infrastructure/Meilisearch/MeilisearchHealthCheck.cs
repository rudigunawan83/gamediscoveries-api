using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Infrastructure.Meilisearch;

public sealed class MeilisearchHealthCheck(IOptions<MeilisearchOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;

        if (!config.Enabled)
        {
            return HealthCheckResult.Degraded("Meilisearch is disabled.");
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await httpClient.GetAsync($"{config.Url.TrimEnd('/')}/health", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Meilisearch is available.")
                : HealthCheckResult.Unhealthy($"Meilisearch returned {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Meilisearch is unavailable.", ex);
        }
    }
}

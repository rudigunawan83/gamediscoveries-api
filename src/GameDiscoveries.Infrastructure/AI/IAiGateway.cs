namespace GameDiscoveries.Infrastructure.AI;

/// <summary>
/// Placeholder for future OpenAI-compatible AI gateway integration.
/// </summary>
public interface IAiGateway
{
    Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}

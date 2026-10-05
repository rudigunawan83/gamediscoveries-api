using GameDiscoveries.Modules.Analytics.Models;

namespace GameDiscoveries.Modules.Analytics.Processing;

/// <summary>
/// Future event consumers (XP, Mission, Achievement, Recommendation, Anti-Abuse)
/// should implement this interface. Phase 01 only establishes the abstraction.
/// </summary>
public interface IAnalyticsEventHandler
{
    string Name { get; }

    Task HandleAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default);
}

public interface IAnalyticsEventDispatcher
{
    Task DispatchAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default);

    Task DispatchManyAsync(
        IReadOnlyList<AnalyticsEventWriteCommand> commands,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight dispatcher. Handlers must remain non-blocking and must not
/// perform expensive XP/recommendation work during Phase 01 ingestion.
/// </summary>
public sealed class AnalyticsEventDispatcher(IEnumerable<IAnalyticsEventHandler> handlers) : IAnalyticsEventDispatcher
{
    public async Task DispatchAsync(AnalyticsEventWriteCommand command, CancellationToken cancellationToken = default)
    {
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(command, cancellationToken);
        }
    }

    public async Task DispatchManyAsync(
        IReadOnlyList<AnalyticsEventWriteCommand> commands,
        CancellationToken cancellationToken = default)
    {
        foreach (var command in commands)
        {
            await DispatchAsync(command, cancellationToken);
        }
    }
}

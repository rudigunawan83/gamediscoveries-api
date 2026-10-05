namespace GameDiscoveries.BuildingBlocks.Abstractions;

/// <summary>
/// Optional streak snapshot for progress API. Implemented by Modules.Streaks.
/// </summary>
public interface IStreakProgressProvider
{
    Task<StreakProgressSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record StreakProgressSnapshot(
    int Current,
    int Longest,
    string Status,
    bool TodayQualified,
    int FreezeCount,
    int? NextMilestoneDays);

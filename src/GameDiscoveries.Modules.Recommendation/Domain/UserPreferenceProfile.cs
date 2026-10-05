namespace GameDiscoveries.Modules.Recommendation.Domain;

public sealed class UserPreferenceProfile
{
    public Guid? UserId { get; init; }
    public IReadOnlyDictionary<string, double> PreferredCategories { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, double> PreferredTags { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, double> PreferredOrientations { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public double MultiplayerPreference { get; init; }
    public double MobilePreference { get; init; }
    public IReadOnlyList<Guid> FavoriteGameIds { get; init; } = [];
    public IReadOnlyList<Guid> PlayedGameIds { get; init; } = [];
    public IReadOnlyList<Guid> DislikedGameIds { get; init; } = [];
    public IReadOnlyList<Guid> RecentSeedGameIds { get; init; } = [];
    public IReadOnlyList<UserSignal> Signals { get; init; } = [];
    public int TotalInteractions { get; init; }
    public int ProfileLevel { get; init; }
    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsColdStart => ProfileLevel <= 1 && FavoriteGameIds.Count == 0 && PlayedGameIds.Count < 3;
}

public sealed record UserSignal(
    Guid GameId,
    string Kind,
    DateTimeOffset At,
    int StrengthCount,
    int DurationSeconds,
    string? Category,
    IReadOnlyList<string> Tags);

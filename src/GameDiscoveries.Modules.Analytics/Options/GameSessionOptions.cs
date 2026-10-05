namespace GameDiscoveries.Modules.Analytics.Options;

public sealed class GameSessionOptions
{
    public const string SectionName = "GameSession";

    public bool Enabled { get; set; } = true;

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int HeartbeatTimeoutSeconds { get; set; } = 90;

    public int MinimumValidActiveSeconds { get; set; } = 30;

    public int MaximumSessionDurationSeconds { get; set; } = 14_400;

    public bool AllowMultipleGames { get; set; } = true;

    public bool AllowMultipleSameGameSessions { get; set; } = false;

    /// <summary>
    /// Minimum seconds between GAME_SESSION_HEARTBEAT analytics rows.
    /// </summary>
    public int HeartbeatAnalyticsSampleSeconds { get; set; } = 120;

    /// <summary>
    /// Cap credited active delta per heartbeat to avoid clock skew abuse.
    /// </summary>
    public int MaxHeartbeatCreditSeconds { get; set; } = 60;
}

namespace GameDiscoveries.Modules.Analytics.Domain;

public static class GamePlaySessionStatuses
{
    public const string Started = "STARTED";
    public const string Active = "ACTIVE";
    public const string Paused = "PAUSED";
    public const string Ended = "ENDED";
    public const string Invalid = "INVALID";

    public static readonly IReadOnlySet<string> Open = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Started, Active, Paused
    };

    public static bool IsTerminal(string status) =>
        string.Equals(status, Ended, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Invalid, StringComparison.OrdinalIgnoreCase);
}

public static class GamePlaySessionInvalidReasons
{
    public const string SessionTooShort = "SESSION_TOO_SHORT";
    public const string SessionTooLong = "SESSION_TOO_LONG";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string HeartbeatAnomaly = "HEARTBEAT_ANOMALY";
    public const string DuplicateSession = "DUPLICATE_SESSION";
    public const string InvalidGame = "INVALID_GAME";
    public const string SuspiciousActivity = "SUSPICIOUS_ACTIVITY";
    public const string EndBeforeStart = "END_BEFORE_START";
}

public static class GamePlayPauseReasons
{
    public const string AppBackground = "APP_BACKGROUND";
    public const string TabHidden = "TAB_HIDDEN";
    public const string WindowBlur = "WINDOW_BLUR";
    public const string UserPause = "USER_PAUSE";
    public const string NetworkLost = "NETWORK_LOST";
    public const string Unknown = "UNKNOWN";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AppBackground, TabHidden, WindowBlur, UserPause, NetworkLost, Unknown
    };

    public static string Normalize(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && All.Contains(reason)
            ? All.First(x => string.Equals(x, reason, StringComparison.OrdinalIgnoreCase))
            : Unknown;
}

public static class GamePlayEndReasons
{
    public const string UserExit = "USER_EXIT";
    public const string GameClosed = "GAME_CLOSED";
    public const string AppTerminated = "APP_TERMINATED";
    public const string Timeout = "TIMEOUT";
    public const string Navigation = "NAVIGATION";
    public const string MaxDuration = "MAX_DURATION";
    public const string Unknown = "UNKNOWN";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        UserExit, GameClosed, AppTerminated, Timeout, Navigation, MaxDuration, Unknown
    };

    public static string Normalize(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && All.Contains(reason)
            ? All.First(x => string.Equals(x, reason, StringComparison.OrdinalIgnoreCase))
            : Unknown;
}

/// <summary>
/// Pure active-time accounting used by the session service and unit tests.
/// </summary>
public static class GamePlayActiveTimeCalculator
{
    public static long CreditHeartbeatMs(
        DateTimeOffset previousHeartbeatAt,
        DateTimeOffset now,
        int heartbeatTimeoutSeconds,
        int maxCreditSeconds)
    {
        if (now <= previousHeartbeatAt)
        {
            return 0;
        }

        var delta = now - previousHeartbeatAt;
        if (delta.TotalSeconds > heartbeatTimeoutSeconds)
        {
            // Gap exceeds timeout — inactive period is not counted.
            return 0;
        }

        var creditSeconds = Math.Min(delta.TotalSeconds, Math.Max(1, maxCreditSeconds));
        return (long)(creditSeconds * 1000);
    }

    public static int ToActiveSeconds(long accumulatedActiveMs) =>
        (int)Math.Max(0, accumulatedActiveMs / 1000);

    public static int DurationSeconds(DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        if (endedAt < startedAt)
        {
            return 0;
        }

        return (int)Math.Floor((endedAt - startedAt).TotalSeconds);
    }

    public static (bool IsValid, string? InvalidReason) EvaluateValidity(
        int activeSeconds,
        int durationSeconds,
        int minimumValidActiveSeconds,
        int maximumSessionDurationSeconds)
    {
        if (durationSeconds < 0 || activeSeconds < 0 || activeSeconds > durationSeconds + 5)
        {
            return (false, GamePlaySessionInvalidReasons.SuspiciousActivity);
        }

        if (durationSeconds > maximumSessionDurationSeconds)
        {
            return (false, GamePlaySessionInvalidReasons.SessionTooLong);
        }

        if (activeSeconds < minimumValidActiveSeconds)
        {
            return (false, GamePlaySessionInvalidReasons.SessionTooShort);
        }

        return (true, null);
    }
}

-- Cross-platform "continue playing": per-game totals filled from server-ended play sessions.
ALTER TABLE user_play_history
    ADD COLUMN IF NOT EXISTS total_play_seconds BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS play_count INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS last_platform VARCHAR(20) NULL;

-- Marks sessions already folded into user_play_history so each counts once.
ALTER TABLE game_play_sessions
    ADD COLUMN IF NOT EXISTS history_recorded_at TIMESTAMPTZ NULL;

-- Backfill from existing ended sessions of signed-in players.
WITH totals AS (
    SELECT s.user_id,
           s.game_id,
           SUM(s.active_seconds)::bigint AS total_seconds,
           COUNT(*)::int AS sessions,
           MAX(s.active_seconds) AS longest,
           MAX(COALESCE(s.ended_at, s.started_at)) AS last_played,
           (ARRAY_AGG(s.platform ORDER BY COALESCE(s.ended_at, s.started_at) DESC))[1] AS platform
    FROM game_play_sessions s
    INNER JOIN games g ON g.id = s.game_id
    INNER JOIN users u ON u.id = s.user_id
    WHERE s.status = 'ENDED' AND s.history_recorded_at IS NULL
    GROUP BY s.user_id, s.game_id
)
INSERT INTO user_play_history (id, user_id, game_id, played_at, duration_seconds, total_play_seconds, play_count, last_platform)
SELECT gen_random_uuid(), t.user_id, t.game_id, t.last_played, t.longest, t.total_seconds, t.sessions, t.platform
FROM totals t
ON CONFLICT (user_id, game_id) DO UPDATE
SET played_at = GREATEST(user_play_history.played_at, EXCLUDED.played_at),
    duration_seconds = GREATEST(user_play_history.duration_seconds, EXCLUDED.duration_seconds),
    total_play_seconds = EXCLUDED.total_play_seconds,
    play_count = EXCLUDED.play_count,
    last_platform = EXCLUDED.last_platform;

UPDATE game_play_sessions
SET history_recorded_at = (NOW() AT TIME ZONE 'utc')
WHERE status = 'ENDED' AND user_id IS NOT NULL AND history_recorded_at IS NULL;

-- History rows recorded only by clients (no ended session) count as one play.
UPDATE user_play_history
SET total_play_seconds = duration_seconds,
    play_count = 1
WHERE play_count = 0;

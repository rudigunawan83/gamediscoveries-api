-- Bound monitoring overview scans to the selected time window.
CREATE INDEX IF NOT EXISTS ix_game_play_sessions_started_at
    ON game_play_sessions (started_at DESC);

CREATE INDEX IF NOT EXISTS ix_users_created_at
    ON users (created_at DESC);

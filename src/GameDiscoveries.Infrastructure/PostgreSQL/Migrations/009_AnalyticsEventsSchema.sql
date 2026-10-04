CREATE TABLE IF NOT EXISTS analytics_events (
    id              UUID PRIMARY KEY,
    event_name      VARCHAR(100) NOT NULL,
    user_id         UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    session_id      VARCHAR(100) NULL,
    game_id         UUID NULL REFERENCES games(id) ON DELETE SET NULL,
    properties      JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_analytics_events_name_created
    ON analytics_events (event_name, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_analytics_events_user_created
    ON analytics_events (user_id, created_at DESC)
    WHERE user_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_analytics_events_game_created
    ON analytics_events (game_id, created_at DESC)
    WHERE game_id IS NOT NULL;

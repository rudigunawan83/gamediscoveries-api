CREATE TABLE IF NOT EXISTS user_favorites (
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id     UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (user_id, game_id)
);

CREATE INDEX IF NOT EXISTS ix_user_favorites_user_created
    ON user_favorites (user_id, created_at DESC);

CREATE TABLE IF NOT EXISTS user_play_history (
    id                UUID PRIMARY KEY,
    user_id           UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id           UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    played_at         TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    duration_seconds  INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT uq_user_play_history_user_game UNIQUE (user_id, game_id),
    CONSTRAINT ck_user_play_history_duration CHECK (duration_seconds >= 0)
);

CREATE INDEX IF NOT EXISTS ix_user_play_history_user_played
    ON user_play_history (user_id, played_at DESC);

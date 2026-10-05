-- Phase 02: Game Play Session Tracking

CREATE TABLE IF NOT EXISTS game_play_sessions (
    id                              UUID PRIMARY KEY,
    session_id                      VARCHAR(100) NOT NULL,
    user_id                         UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    anonymous_id                    UUID NULL,
    game_id                         UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    status                          VARCHAR(32) NOT NULL,
    started_at                      TIMESTAMPTZ NOT NULL,
    last_heartbeat_at               TIMESTAMPTZ NULL,
    paused_at                       TIMESTAMPTZ NULL,
    resumed_at                      TIMESTAMPTZ NULL,
    ended_at                        TIMESTAMPTZ NULL,
    duration_seconds                INTEGER NOT NULL DEFAULT 0,
    active_seconds                  INTEGER NOT NULL DEFAULT 0,
    accumulated_active_ms           BIGINT NOT NULL DEFAULT 0,
    is_valid                        BOOLEAN NOT NULL DEFAULT FALSE,
    invalid_reason                  VARCHAR(64) NULL,
    source                          VARCHAR(32) NOT NULL,
    platform                        VARCHAR(32) NOT NULL,
    device_type                     VARCHAR(32) NULL,
    app_version                     VARCHAR(64) NULL,
    pause_reason                    VARCHAR(64) NULL,
    end_reason                      VARCHAR(64) NULL,
    last_heartbeat_analytics_at     TIMESTAMPTZ NULL,
    heartbeat_count                 INTEGER NOT NULL DEFAULT 0,
    created_at                      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at                      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ck_game_play_sessions_status CHECK (
        status IN ('STARTED', 'ACTIVE', 'PAUSED', 'ENDED', 'INVALID')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_game_play_sessions_session_id
    ON game_play_sessions (session_id);

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_game_started
    ON game_play_sessions (game_id, started_at DESC);

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_user_started
    ON game_play_sessions (user_id, started_at DESC)
    WHERE user_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_anonymous_started
    ON game_play_sessions (anonymous_id, started_at DESC)
    WHERE anonymous_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_user_game_started
    ON game_play_sessions (user_id, game_id, started_at DESC)
    WHERE user_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_ended
    ON game_play_sessions (ended_at DESC)
    WHERE ended_at IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_is_valid
    ON game_play_sessions (is_valid, started_at DESC);

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_last_heartbeat
    ON game_play_sessions (last_heartbeat_at DESC)
    WHERE last_heartbeat_at IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_active_status
    ON game_play_sessions (game_id, status)
    WHERE status IN ('STARTED', 'ACTIVE', 'PAUSED');

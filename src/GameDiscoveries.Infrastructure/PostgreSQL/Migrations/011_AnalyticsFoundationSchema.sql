-- Phase 01: Event & Analytics Foundation
-- Extends analytics_events for idempotent ingest + session foundation.

ALTER TABLE analytics_events
    ADD COLUMN IF NOT EXISTS event_id UUID,
    ADD COLUMN IF NOT EXISTS anonymous_id UUID,
    ADD COLUMN IF NOT EXISTS source VARCHAR(32),
    ADD COLUMN IF NOT EXISTS platform VARCHAR(32),
    ADD COLUMN IF NOT EXISTS device_type VARCHAR(32),
    ADD COLUMN IF NOT EXISTS app_version VARCHAR(64),
    ADD COLUMN IF NOT EXISTS page_url VARCHAR(2048),
    ADD COLUMN IF NOT EXISTS referrer_url VARCHAR(2048),
    ADD COLUMN IF NOT EXISTS ip_hash VARCHAR(128),
    ADD COLUMN IF NOT EXISTS user_agent VARCHAR(512),
    ADD COLUMN IF NOT EXISTS occurred_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS received_at TIMESTAMPTZ;

UPDATE analytics_events
SET event_id = id
WHERE event_id IS NULL;

UPDATE analytics_events
SET occurred_at = created_at
WHERE occurred_at IS NULL;

UPDATE analytics_events
SET received_at = created_at
WHERE received_at IS NULL;

UPDATE analytics_events
SET source = COALESCE(source, 'WEB')
WHERE source IS NULL;

UPDATE analytics_events
SET platform = COALESCE(platform, 'WEB')
WHERE platform IS NULL;

ALTER TABLE analytics_events
    ALTER COLUMN event_id SET NOT NULL,
    ALTER COLUMN occurred_at SET NOT NULL,
    ALTER COLUMN received_at SET NOT NULL,
    ALTER COLUMN source SET NOT NULL,
    ALTER COLUMN platform SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_analytics_events_event_id
    ON analytics_events (event_id);

CREATE INDEX IF NOT EXISTS ix_analytics_events_anonymous_created
    ON analytics_events (anonymous_id, occurred_at DESC)
    WHERE anonymous_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_analytics_events_session_occurred
    ON analytics_events (session_id, occurred_at DESC)
    WHERE session_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_analytics_events_type_occurred
    ON analytics_events (event_name, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_analytics_events_game_type_occurred
    ON analytics_events (game_id, event_name, occurred_at DESC)
    WHERE game_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_analytics_events_source_platform
    ON analytics_events (source, platform, occurred_at DESC);

CREATE TABLE IF NOT EXISTS analytics_sessions (
    id                  UUID PRIMARY KEY,
    session_id          VARCHAR(100) NOT NULL,
    user_id             UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    anonymous_id        UUID NULL,
    source              VARCHAR(32) NOT NULL,
    platform            VARCHAR(32) NOT NULL,
    started_at          TIMESTAMPTZ NOT NULL,
    last_activity_at    TIMESTAMPTZ NOT NULL,
    ended_at            TIMESTAMPTZ NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_analytics_sessions_session_id
    ON analytics_sessions (session_id);

CREATE INDEX IF NOT EXISTS ix_analytics_sessions_user_activity
    ON analytics_sessions (user_id, last_activity_at DESC)
    WHERE user_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_analytics_sessions_anonymous_activity
    ON analytics_sessions (anonymous_id, last_activity_at DESC)
    WHERE anonymous_id IS NOT NULL;

-- Identity association: AnonymousId → UserId (do not rewrite historical events)
CREATE TABLE IF NOT EXISTS analytics_identity_links (
    id              UUID PRIMARY KEY,
    anonymous_id    UUID NOT NULL,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    first_seen_at   TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    last_seen_at    TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ux_analytics_identity_links UNIQUE (anonymous_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_analytics_identity_links_user
    ON analytics_identity_links (user_id, last_seen_at DESC);

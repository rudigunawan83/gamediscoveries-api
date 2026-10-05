-- Phase 06: Streak System

ALTER TABLE user_progress
    ADD COLUMN IF NOT EXISTS streak_start_date DATE NULL,
    ADD COLUMN IF NOT EXISTS last_qualifying_activity_date DATE NULL,
    ADD COLUMN IF NOT EXISTS streak_status VARCHAR(16) NOT NULL DEFAULT 'BROKEN',
    ADD COLUMN IF NOT EXISTS streak_freeze_count INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS streak_updated_at TIMESTAMPTZ NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_user_progress_streak_status'
    ) THEN
        ALTER TABLE user_progress
            ADD CONSTRAINT ck_user_progress_streak_status
            CHECK (streak_status IN ('ACTIVE', 'AT_RISK', 'BROKEN', 'FROZEN'));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_user_progress_freeze_count'
    ) THEN
        ALTER TABLE user_progress
            ADD CONSTRAINT ck_user_progress_freeze_count
            CHECK (streak_freeze_count >= 0);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS user_activity_days (
    id                          UUID PRIMARY KEY,
    user_id                     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    activity_date               DATE NOT NULL,
    first_activity_at           TIMESTAMPTZ NOT NULL,
    last_activity_at            TIMESTAMPTZ NOT NULL,
    qualifying_session_count    INTEGER NOT NULL DEFAULT 1,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_user_activity_days_user_date UNIQUE (user_id, activity_date),
    CONSTRAINT ck_user_activity_days_session_count CHECK (qualifying_session_count >= 1)
);

CREATE INDEX IF NOT EXISTS ix_user_activity_days_user_date
    ON user_activity_days (user_id, activity_date DESC);

CREATE TABLE IF NOT EXISTS streak_history (
    id                  UUID PRIMARY KEY,
    user_id             UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    event_type          VARCHAR(32) NOT NULL,
    streak_value        INTEGER NOT NULL DEFAULT 0,
    activity_date       DATE NULL,
    previous_streak     INTEGER NULL,
    new_streak          INTEGER NULL,
    reason              VARCHAR(255) NULL,
    metadata_json       JSONB NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ck_streak_history_event CHECK (
        event_type IN (
            'STREAK_STARTED', 'STREAK_CONTINUED', 'STREAK_MILESTONE',
            'STREAK_FROZEN', 'STREAK_BROKEN', 'STREAK_RECOVERED',
            'ADMIN_STREAK_RESET', 'ADMIN_FREEZE_GRANTED', 'ADMIN_FREEZE_REMOVED'
        )
    )
);

CREATE INDEX IF NOT EXISTS ix_streak_history_user_created
    ON streak_history (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_streak_history_user_activity
    ON streak_history (user_id, activity_date DESC);

CREATE TABLE IF NOT EXISTS streak_milestones (
    id              UUID PRIMARY KEY,
    days            INTEGER NOT NULL,
    title           VARCHAR(100) NOT NULL,
    description     VARCHAR(255) NULL,
    reward_xp       INTEGER NULL,
    reward_type     VARCHAR(64) NULL,
    reward_value    VARCHAR(100) NULL,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_streak_milestones_days UNIQUE (days),
    CONSTRAINT ck_streak_milestones_days CHECK (days > 0),
    CONSTRAINT ck_streak_milestones_reward CHECK (reward_xp IS NULL OR reward_xp >= 0)
);

CREATE TABLE IF NOT EXISTS user_streak_milestones (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    days            INTEGER NOT NULL,
    achieved_at     TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    reward_xp       INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT uq_user_streak_milestones UNIQUE (user_id, days)
);

CREATE INDEX IF NOT EXISTS ix_user_streak_milestones_user
    ON user_streak_milestones (user_id, days);

INSERT INTO streak_milestones (id, days, title, description, reward_xp, is_active)
VALUES
    ('b1000006-0001-4000-8000-000000000003', 3, 'Getting Started', '3-day streak', 0, TRUE),
    ('b1000006-0001-4000-8000-000000000007', 7, 'Week Warrior', '7-day streak', 50, TRUE),
    ('b1000006-0001-4000-8000-000000000014', 14, 'Two Week Champion', '14-day streak', 100, TRUE),
    ('b1000006-0001-4000-8000-000000000030', 30, 'Monthly Legend', '30-day streak', 200, TRUE),
    ('b1000006-0001-4000-8000-000000000060', 60, 'Dedicated Discoverer', '60-day streak', 300, TRUE),
    ('b1000006-0001-4000-8000-000000000100', 100, 'Century Streak', '100-day streak', 500, TRUE),
    ('b1000006-0001-4000-8000-000000000365', 365, 'Year of Discovery', '365-day streak', 1000, TRUE)
ON CONFLICT (days) DO NOTHING;

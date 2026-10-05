-- Phase 04: Levels, progress extensions, level history, audit logs

ALTER TABLE user_progress
    ADD COLUMN IF NOT EXISTS games_played INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS favorites_count INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS unique_games_played INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS last_activity_at TIMESTAMPTZ NULL;

CREATE TABLE IF NOT EXISTS gamification_levels (
    id                  UUID PRIMARY KEY,
    level               INTEGER NOT NULL,
    required_total_xp   BIGINT NOT NULL,
    title               VARCHAR(100) NOT NULL,
    description         VARCHAR(255) NULL,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ux_gamification_levels_level UNIQUE (level),
    CONSTRAINT ux_gamification_levels_required_xp UNIQUE (required_total_xp),
    CONSTRAINT ck_gamification_levels_level CHECK (level >= 1 AND level <= 1000)
);

CREATE INDEX IF NOT EXISTS ix_gamification_levels_active_level
    ON gamification_levels (is_active, level);

CREATE TABLE IF NOT EXISTS user_level_history (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    old_level       INTEGER NOT NULL,
    new_level       INTEGER NOT NULL,
    total_xp        BIGINT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_user_level_history_user_created
    ON user_level_history (user_id, created_at DESC);

CREATE TABLE IF NOT EXISTS audit_logs (
    id              UUID PRIMARY KEY,
    admin_id        UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    action          VARCHAR(64) NOT NULL,
    target_type     VARCHAR(64) NOT NULL,
    target_id       VARCHAR(100) NOT NULL,
    before_json     JSONB NULL,
    after_json      JSONB NULL,
    reason          VARCHAR(500) NULL,
    ip_hash         VARCHAR(128) NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_admin_created
    ON audit_logs (admin_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_audit_logs_target
    ON audit_logs (target_type, target_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_audit_logs_action_created
    ON audit_logs (action, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_user_progress_total_xp
    ON user_progress (total_xp DESC);

CREATE INDEX IF NOT EXISTS ix_user_progress_level
    ON user_progress (level);

-- Seed levels 1..100 (incremental curve: +50*n XP to reach level n)
DO $$
DECLARE
    n INT;
    req BIGINT := 0;
    lvl_title TEXT;
    lvl_desc TEXT;
BEGIN
    FOR n IN 1..100 LOOP
        IF n = 1 THEN
            req := 0;
        ELSE
            req := req + (50 * n);
        END IF;

        IF n >= 100 THEN
            lvl_title := 'Ultimate Discoverer';
            lvl_desc := 'Peak of discovery.';
        ELSIF n >= 50 THEN
            lvl_title := 'Game Legend';
            lvl_desc := 'A legendary explorer of games.';
        ELSIF n >= 30 THEN
            lvl_title := 'Game Master';
            lvl_desc := 'Mastery through play.';
        ELSIF n >= 20 THEN
            lvl_title := 'Adventurer';
            lvl_desc := 'Deep into the catalog.';
        ELSIF n >= 10 THEN
            lvl_title := 'Game Hunter';
            lvl_desc := 'Actively hunting great games.';
        ELSIF n >= 5 THEN
            lvl_title := 'Explorer';
            lvl_desc := 'Discovering new experiences.';
        ELSE
            lvl_title := 'Newcomer';
            lvl_desc := 'Just getting started.';
        END IF;

        INSERT INTO gamification_levels (id, level, required_total_xp, title, description, is_active, created_at, updated_at)
        VALUES (gen_random_uuid(), n, req, lvl_title, lvl_desc, TRUE,
                (NOW() AT TIME ZONE 'utc'), (NOW() AT TIME ZONE 'utc'))
        ON CONFLICT (level) DO NOTHING;
    END LOOP;
END $$;

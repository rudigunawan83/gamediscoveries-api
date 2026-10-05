-- Phase 05: Daily Mission & Weekly Challenge

CREATE TABLE IF NOT EXISTS mission_templates (
    id                  UUID PRIMARY KEY,
    code                VARCHAR(64) NOT NULL,
    type                VARCHAR(16) NOT NULL,
    title               VARCHAR(200) NOT NULL,
    description         VARCHAR(500) NOT NULL,
    icon                VARCHAR(64) NULL,
    requirement_type    VARCHAR(64) NOT NULL,
    target_value        INTEGER NOT NULL,
    reward_xp           INTEGER NOT NULL,
    difficulty          VARCHAR(16) NOT NULL,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order          INTEGER NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_mission_templates_code UNIQUE (code),
    CONSTRAINT ck_mission_templates_type CHECK (type IN ('DAILY', 'WEEKLY')),
    CONSTRAINT ck_mission_templates_difficulty CHECK (difficulty IN ('EASY', 'MEDIUM', 'HARD')),
    CONSTRAINT ck_mission_templates_target CHECK (target_value > 0),
    CONSTRAINT ck_mission_templates_reward CHECK (reward_xp >= 0)
);

CREATE INDEX IF NOT EXISTS ix_mission_templates_type_active
    ON mission_templates (type, is_active, difficulty, sort_order);

CREATE TABLE IF NOT EXISTS user_missions (
    id                      UUID PRIMARY KEY,
    user_id                 UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    mission_template_id     UUID NOT NULL REFERENCES mission_templates(id),
    code                    VARCHAR(64) NOT NULL,
    type                    VARCHAR(16) NOT NULL,
    title                   VARCHAR(200) NOT NULL,
    description             VARCHAR(500) NOT NULL,
    requirement_type        VARCHAR(64) NOT NULL,
    period_start            TIMESTAMPTZ NOT NULL,
    period_end              TIMESTAMPTZ NOT NULL,
    progress_value          INTEGER NOT NULL DEFAULT 0,
    target_value            INTEGER NOT NULL,
    reward_xp               INTEGER NOT NULL,
    status                  VARCHAR(16) NOT NULL DEFAULT 'ACTIVE',
    completed_at            TIMESTAMPTZ NULL,
    reward_transaction_id   UUID NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_user_missions_user_template_period UNIQUE (user_id, mission_template_id, period_start),
    CONSTRAINT ck_user_missions_type CHECK (type IN ('DAILY', 'WEEKLY')),
    CONSTRAINT ck_user_missions_status CHECK (status IN ('ACTIVE', 'COMPLETED', 'EXPIRED', 'CANCELLED')),
    CONSTRAINT ck_user_missions_progress CHECK (progress_value >= 0),
    CONSTRAINT ck_user_missions_target CHECK (target_value > 0)
);

CREATE INDEX IF NOT EXISTS ix_user_missions_user_period
    ON user_missions (user_id, period_start DESC);

CREATE INDEX IF NOT EXISTS ix_user_missions_user_status
    ON user_missions (user_id, status);

CREATE INDEX IF NOT EXISTS ix_user_missions_period_end_status
    ON user_missions (period_end, status);

CREATE INDEX IF NOT EXISTS ix_user_missions_user_type_period
    ON user_missions (user_id, type, period_start DESC);

-- Seed daily templates
INSERT INTO mission_templates (id, code, type, title, description, icon, requirement_type, target_value, reward_xp, difficulty, is_active, sort_order)
VALUES
    ('a1000001-0001-4000-8000-000000000001', 'PLAY_2_GAMES', 'DAILY', 'Play 2 Games',
     'Play 2 different games today.', 'gamepad', 'UNIQUE_GAMES_PLAYED', 2, 50, 'MEDIUM', TRUE, 10),
    ('a1000001-0001-4000-8000-000000000002', 'DISCOVER_NEW_GAME', 'DAILY', 'Discover a New Game',
     'Play a game you have never played before.', 'search', 'NEW_GAME_DISCOVERED', 1, 50, 'MEDIUM', TRUE, 20),
    ('a1000001-0001-4000-8000-000000000003', 'EXPLORE_NEW_GENRE', 'DAILY', 'Explore a New Genre',
     'Try a game from a genre you have never played before.', 'compass', 'NEW_GENRE_DISCOVERED', 1, 50, 'HARD', TRUE, 30),
    ('a1000001-0001-4000-8000-000000000004', 'PLAY_10_MINUTES', 'DAILY', 'Play for 10 Minutes',
     'Accumulate 10 minutes of valid active game time today.', 'clock', 'ACTIVE_TIME_SECONDS', 600, 50, 'MEDIUM', TRUE, 40),
    ('a1000001-0001-4000-8000-000000000005', 'FAVORITE_A_GAME', 'DAILY', 'Save a Favorite',
     'Add a game to your favorites.', 'heart', 'FAVORITES_ADDED', 1, 50, 'EASY', TRUE, 50)
ON CONFLICT (code) DO NOTHING;

-- Seed weekly templates
INSERT INTO mission_templates (id, code, type, title, description, icon, requirement_type, target_value, reward_xp, difficulty, is_active, sort_order)
VALUES
    ('a1000001-0002-4000-8000-000000000001', 'PLAY_5_DAYS', 'WEEKLY', 'Weekly Explorer',
     'Play at least one valid game session on 5 different days this week.', 'calendar', 'ACTIVE_DAYS', 5, 150, 'HARD', TRUE, 10),
    ('a1000001-0002-4000-8000-000000000002', 'PLAY_5_GAMES', 'WEEKLY', 'Game Collector',
     'Play 5 different games this week.', 'collection', 'UNIQUE_GAMES_PLAYED', 5, 150, 'MEDIUM', TRUE, 20),
    ('a1000001-0002-4000-8000-000000000003', 'EXPLORE_5_GENRES', 'WEEKLY', 'Genre Explorer',
     'Discover games from 5 different genres this week.', 'map', 'UNIQUE_GENRES_PLAYED', 5, 150, 'HARD', TRUE, 30),
    ('a1000001-0002-4000-8000-000000000004', 'PLAY_60_MINUTES', 'WEEKLY', 'Dedicated Player',
     'Accumulate 60 minutes of valid active game time this week.', 'timer', 'ACTIVE_TIME_SECONDS', 3600, 150, 'MEDIUM', TRUE, 40),
    ('a1000001-0002-4000-8000-000000000005', 'DISCOVER_10_GAMES', 'WEEKLY', 'Ultimate Explorer',
     'Play 10 different games this week.', 'trophy', 'UNIQUE_GAMES_PLAYED', 10, 150, 'HARD', TRUE, 50)
ON CONFLICT (code) DO NOTHING;

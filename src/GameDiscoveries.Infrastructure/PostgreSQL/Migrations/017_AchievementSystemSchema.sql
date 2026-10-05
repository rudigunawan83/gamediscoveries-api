-- Phase 07: Achievement & Badge System

CREATE TABLE IF NOT EXISTS achievement_definitions (
    id                  UUID PRIMARY KEY,
    code                VARCHAR(80) NOT NULL,
    title               VARCHAR(160) NOT NULL,
    description         VARCHAR(500) NOT NULL,
    short_description   VARCHAR(255) NULL,
    badge_image_url     VARCHAR(500) NULL,
    category            VARCHAR(32) NOT NULL,
    difficulty          VARCHAR(32) NOT NULL,
    requirement_type    VARCHAR(64) NOT NULL,
    target_value        INTEGER NOT NULL,
    reward_xp           INTEGER NOT NULL DEFAULT 0,
    icon                VARCHAR(64) NULL,
    is_secret           BOOLEAN NOT NULL DEFAULT FALSE,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    season_id           UUID NULL,
    sort_order          INTEGER NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_achievement_definitions_code UNIQUE (code),
    CONSTRAINT ck_achievement_definitions_category CHECK (
        category IN ('DISCOVERY', 'GAMEPLAY', 'EXPLORATION', 'SOCIAL', 'COLLECTION', 'STREAK', 'PROGRESSION', 'SPECIAL')
    ),
    CONSTRAINT ck_achievement_definitions_difficulty CHECK (
        difficulty IN ('EASY', 'MEDIUM', 'HARD', 'EPIC', 'LEGENDARY')
    ),
    CONSTRAINT ck_achievement_definitions_requirement CHECK (
        requirement_type IN (
            'FIRST_GAME_PLAYED', 'GAMES_PLAYED', 'UNIQUE_GAMES_PLAYED', 'GAMES_DISCOVERED',
            'UNIQUE_GENRES_PLAYED', 'FAVORITES_COUNT', 'RATINGS_COUNT', 'REVIEWS_COUNT',
            'TOTAL_ACTIVE_TIME', 'VALID_SESSIONS_COUNT', 'STREAK_DAYS', 'LONGEST_STREAK',
            'CURRENT_LEVEL', 'TOTAL_XP',
            'DAILY_MISSIONS_COMPLETED', 'WEEKLY_CHALLENGES_COMPLETED',
            'ACHIEVEMENT_COUNT', 'SPECIAL_CONDITION'
        )
    ),
    CONSTRAINT ck_achievement_definitions_target CHECK (target_value > 0),
    CONSTRAINT ck_achievement_definitions_reward CHECK (reward_xp >= 0)
);

ALTER TABLE achievement_definitions
    ADD COLUMN IF NOT EXISTS short_description VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS badge_image_url VARCHAR(500) NULL;

ALTER TABLE achievement_definitions
    DROP CONSTRAINT IF EXISTS ck_achievement_definitions_category,
    DROP CONSTRAINT IF EXISTS ck_achievement_definitions_difficulty,
    DROP CONSTRAINT IF EXISTS ck_achievement_definitions_requirement;

ALTER TABLE achievement_definitions
    ADD CONSTRAINT ck_achievement_definitions_category CHECK (
        category IN ('DISCOVERY', 'GAMEPLAY', 'EXPLORATION', 'SOCIAL', 'COLLECTION', 'STREAK', 'PROGRESSION', 'SPECIAL')
    ),
    ADD CONSTRAINT ck_achievement_definitions_difficulty CHECK (
        difficulty IN ('EASY', 'MEDIUM', 'HARD', 'EPIC', 'LEGENDARY')
    ),
    ADD CONSTRAINT ck_achievement_definitions_requirement CHECK (
        requirement_type IN (
            'FIRST_GAME_PLAYED', 'GAMES_PLAYED', 'UNIQUE_GAMES_PLAYED', 'GAMES_DISCOVERED',
            'UNIQUE_GENRES_PLAYED', 'FAVORITES_COUNT', 'RATINGS_COUNT', 'REVIEWS_COUNT',
            'TOTAL_ACTIVE_TIME', 'VALID_SESSIONS_COUNT', 'STREAK_DAYS', 'LONGEST_STREAK',
            'CURRENT_LEVEL', 'TOTAL_XP',
            'DAILY_MISSIONS_COMPLETED', 'WEEKLY_CHALLENGES_COMPLETED',
            'ACHIEVEMENT_COUNT', 'SPECIAL_CONDITION'
        )
    );

CREATE INDEX IF NOT EXISTS ix_achievement_definitions_active_sort
    ON achievement_definitions (is_active, sort_order, created_at);

CREATE INDEX IF NOT EXISTS ix_achievement_definitions_requirement
    ON achievement_definitions (requirement_type)
    WHERE is_active = TRUE;

CREATE INDEX IF NOT EXISTS ix_achievement_definitions_category
    ON achievement_definitions (category, difficulty);

CREATE TABLE IF NOT EXISTS user_achievement_unlocks (
    id                          UUID PRIMARY KEY,
    user_id                     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    achievement_definition_id   UUID NOT NULL REFERENCES achievement_definitions(id) ON DELETE CASCADE,
    unlocked_at                 TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    progress_value              INTEGER NOT NULL DEFAULT 0,
    target_value                INTEGER NOT NULL,
    reward_transaction_id       UUID NULL,
    is_notified                 BOOLEAN NOT NULL DEFAULT FALSE,
    granted_by_admin_id         UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    revoked_at                  TIMESTAMPTZ NULL,
    revoked_by_admin_id         UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    revoke_reason               VARCHAR(500) NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_user_achievement_unlocks_user_definition UNIQUE (user_id, achievement_definition_id),
    CONSTRAINT ck_user_achievement_unlocks_progress CHECK (progress_value >= 0),
    CONSTRAINT ck_user_achievement_unlocks_target CHECK (target_value > 0)
);

CREATE INDEX IF NOT EXISTS ix_user_achievement_unlocks_user_unlocked
    ON user_achievement_unlocks (user_id, unlocked_at DESC)
    WHERE revoked_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_user_achievement_unlocks_definition_unlocked
    ON user_achievement_unlocks (achievement_definition_id, unlocked_at DESC)
    WHERE revoked_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_user_achievement_unlocks_revoked
    ON user_achievement_unlocks (revoked_at)
    WHERE revoked_at IS NOT NULL;

CREATE TABLE IF NOT EXISTS achievement_history (
    id                          UUID PRIMARY KEY,
    user_id                     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    achievement_definition_id   UUID NOT NULL REFERENCES achievement_definitions(id) ON DELETE CASCADE,
    event_type                  VARCHAR(32) NOT NULL,
    admin_id                    UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    reason                      VARCHAR(500) NULL,
    metadata_json               JSONB NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ck_achievement_history_event CHECK (
        event_type IN ('UNLOCKED', 'GRANTED', 'REVOKED', 'ACTIVATED', 'DEACTIVATED', 'CREATED', 'UPDATED')
    )
);

CREATE INDEX IF NOT EXISTS ix_achievement_history_user_created
    ON achievement_history (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_achievement_history_definition_created
    ON achievement_history (achievement_definition_id, created_at DESC);

INSERT INTO achievement_definitions (
    id, code, title, description, short_description, category, difficulty, requirement_type,
    target_value, reward_xp, icon, is_secret, is_active, sort_order)
VALUES
    ('b1000007-0001-4000-8000-000000000001', 'FIRST_DISCOVERY', 'First Discovery',
     'Play your first game and begin your discovery journey.', 'Play your first game.', 'DISCOVERY', 'EASY', 'FIRST_GAME_PLAYED', 1, 20, 'compass', FALSE, TRUE, 10),
    ('b1000007-0001-4000-8000-000000000002', 'GAME_EXPLORER', 'Game Explorer',
     'Play 5 unique games and broaden your discovery list.', 'Play 5 unique games.', 'DISCOVERY', 'EASY', 'UNIQUE_GAMES_PLAYED', 5, 50, 'gamepad', FALSE, TRUE, 20),
    ('b1000007-0001-4000-8000-000000000003', 'GAME_COLLECTOR', 'Game Collector',
     'Play 25 unique games and build a growing collection of discoveries.', 'Play 25 unique games.', 'DISCOVERY', 'MEDIUM', 'UNIQUE_GAMES_PLAYED', 25, 100, 'collection', FALSE, TRUE, 30),
    ('b1000007-0001-4000-8000-000000000004', 'GAME_HUNTER', 'Game Hunter',
     'Play 50 unique games and prove your dedication to finding new experiences.', 'Play 50 unique games.', 'DISCOVERY', 'HARD', 'UNIQUE_GAMES_PLAYED', 50, 200, 'search', FALSE, TRUE, 40),
    ('b1000007-0001-4000-8000-000000000005', 'ULTIMATE_DISCOVERER', 'Ultimate Discoverer',
     'Play 100 unique games and become the ultimate discoverer.', 'Play 100 unique games.', 'DISCOVERY', 'LEGENDARY', 'UNIQUE_GAMES_PLAYED', 100, 500, 'trophy', TRUE, TRUE, 50),
    ('b1000007-0001-4000-8000-000000000006', 'GENRE_EXPLORER', 'Genre Explorer',
     'Play games from 3 different genres and start exploring beyond your comfort zone.', 'Play games from 3 genres.', 'EXPLORATION', 'EASY', 'UNIQUE_GENRES_PLAYED', 3, 50, 'map', FALSE, TRUE, 60),
    ('b1000007-0001-4000-8000-000000000007', 'GENRE_MASTER', 'Genre Master',
     'Play games from 10 different genres and master a wide range of experiences.', 'Play games from 10 genres.', 'EXPLORATION', 'MEDIUM', 'UNIQUE_GENRES_PLAYED', 10, 150, 'compass', FALSE, TRUE, 70),
    ('b1000007-0001-4000-8000-000000000008', 'GENRE_LEGEND', 'Genre Legend',
     'Play games from 20 different genres and become a legend of exploration.', 'Play games from 20 genres.', 'EXPLORATION', 'HARD', 'UNIQUE_GENRES_PLAYED', 20, 300, 'globe', FALSE, TRUE, 80),
    ('b1000007-0001-4000-8000-000000000009', 'FIRST_SESSION', 'First Session',
     'Complete your first valid play session.', 'Complete your first valid session.', 'GAMEPLAY', 'EASY', 'VALID_SESSIONS_COUNT', 1, 20, 'play', FALSE, TRUE, 90),
    ('b1000007-0001-4000-8000-000000000010', 'PLAYFUL', 'Playful',
     'Complete 10 valid play sessions.', 'Complete 10 valid sessions.', 'GAMEPLAY', 'EASY', 'VALID_SESSIONS_COUNT', 10, 50, 'gamepad', FALSE, TRUE, 100),
    ('b1000007-0001-4000-8000-000000000011', 'DEDICATED_PLAYER', 'Dedicated Player',
     'Complete 50 valid play sessions and show consistent dedication.', 'Complete 50 valid sessions.', 'GAMEPLAY', 'MEDIUM', 'VALID_SESSIONS_COUNT', 50, 150, 'target', FALSE, TRUE, 110),
    ('b1000007-0001-4000-8000-000000000012', 'MARATHON_PLAYER', 'Marathon Player',
     'Accumulate 1 hour of valid active play time.', 'Play actively for 1 hour.', 'GAMEPLAY', 'MEDIUM', 'TOTAL_ACTIVE_TIME', 3600, 100, 'clock', FALSE, TRUE, 120),
    ('b1000007-0001-4000-8000-000000000013', 'TIME_MASTER', 'Time Master',
     'Accumulate 10 hours of valid active play time.', 'Play actively for 10 hours.', 'GAMEPLAY', 'HARD', 'TOTAL_ACTIVE_TIME', 36000, 300, 'hourglass', FALSE, TRUE, 130),
    ('b1000007-0001-4000-8000-000000000014', 'FIRST_FAVORITE', 'First Favorite',
     'Add your first favorite game.', 'Add your first favorite.', 'COLLECTION', 'EASY', 'FAVORITES_COUNT', 1, 20, 'heart', FALSE, TRUE, 140),
    ('b1000007-0001-4000-8000-000000000015', 'COLLECTOR', 'Collector',
     'Favorite 10 games and start shaping your personal collection.', 'Favorite 10 games.', 'COLLECTION', 'MEDIUM', 'FAVORITES_COUNT', 10, 100, 'bookmark', FALSE, TRUE, 150),
    ('b1000007-0001-4000-8000-000000000016', 'SUPER_COLLECTOR', 'Super Collector',
     'Favorite 50 games and build an impressive collection.', 'Favorite 50 games.', 'COLLECTION', 'HARD', 'FAVORITES_COUNT', 50, 250, 'archive', FALSE, TRUE, 160),
    ('b1000007-0001-4000-8000-000000000017', 'FIRST_RATING', 'First Rating',
     'Rate your first game and share your opinion.', 'Rate your first game.', 'SOCIAL', 'EASY', 'RATINGS_COUNT', 1, 20, 'star', FALSE, TRUE, 170),
    ('b1000007-0001-4000-8000-000000000018', 'GAME_CRITIC', 'Game Critic',
     'Rate 10 games and become a trusted voice in the community.', 'Rate 10 games.', 'SOCIAL', 'MEDIUM', 'RATINGS_COUNT', 10, 100, 'stars', FALSE, TRUE, 180),
    ('b1000007-0001-4000-8000-000000000019', 'FIRST_REVIEW', 'First Review',
     'Write your first published review.', 'Write your first review.', 'SOCIAL', 'EASY', 'REVIEWS_COUNT', 1, 30, 'message', FALSE, TRUE, 190),
    ('b1000007-0001-4000-8000-000000000020', 'REVIEWER', 'Reviewer',
     'Write 10 published reviews and help others discover great games.', 'Write 10 reviews.', 'SOCIAL', 'MEDIUM', 'REVIEWS_COUNT', 10, 150, 'messages', FALSE, TRUE, 200),
    ('b1000007-0001-4000-8000-000000000021', 'THREE_DAY_STREAK', 'Three Day Streak',
     'Reach a 3 day streak.', 'Reach a 3 day streak.', 'STREAK', 'EASY', 'STREAK_DAYS', 3, 30, 'flame', FALSE, TRUE, 210),
    ('b1000007-0001-4000-8000-000000000022', 'WEEK_WARRIOR', 'Week Warrior',
     'Reach a 7 day streak.', 'Reach a 7 day streak.', 'STREAK', 'EASY', 'STREAK_DAYS', 7, 75, 'calendar', FALSE, TRUE, 220),
    ('b1000007-0001-4000-8000-000000000023', 'TWO_WEEK_STREAK', 'Two Week Streak',
     'Reach a 14 day streak.', 'Reach a 14 day streak.', 'STREAK', 'MEDIUM', 'STREAK_DAYS', 14, 150, 'calendar-days', FALSE, TRUE, 230),
    ('b1000007-0001-4000-8000-000000000024', 'MONTH_WARRIOR', 'Month Warrior',
     'Reach a 30 day streak.', 'Reach a 30 day streak.', 'STREAK', 'HARD', 'STREAK_DAYS', 30, 300, 'fire', FALSE, TRUE, 240),
    ('b1000007-0001-4000-8000-000000000025', 'STREAK_LEGEND', 'Streak Legend',
     'Reach a 100 day streak and become a streak legend.', 'Reach a 100 day streak.', 'STREAK', 'LEGENDARY', 'STREAK_DAYS', 100, 1000, 'flame-kindling', TRUE, TRUE, 250),
    ('b1000007-0001-4000-8000-000000000026', 'LEVEL_5', 'Level 5',
     'Reach level 5.', 'Reach level 5.', 'PROGRESSION', 'EASY', 'CURRENT_LEVEL', 5, 50, 'level-up', FALSE, TRUE, 260),
    ('b1000007-0001-4000-8000-000000000027', 'LEVEL_10', 'Level 10',
     'Reach level 10.', 'Reach level 10.', 'PROGRESSION', 'MEDIUM', 'CURRENT_LEVEL', 10, 100, 'level-up', FALSE, TRUE, 270),
    ('b1000007-0001-4000-8000-000000000028', 'LEVEL_20', 'Level 20',
     'Reach level 20.', 'Reach level 20.', 'PROGRESSION', 'HARD', 'CURRENT_LEVEL', 20, 250, 'level-up', FALSE, TRUE, 280),
    ('b1000007-0001-4000-8000-000000000029', 'LEVEL_50', 'Level 50',
     'Reach level 50 and prove your long-term progression.', 'Reach level 50.', 'PROGRESSION', 'LEGENDARY', 'CURRENT_LEVEL', 50, 1000, 'trophy', TRUE, TRUE, 290),
    ('b1000007-0001-4000-8000-000000000030', 'MISSION_ROOKIE', 'Mission Rookie',
     'Complete 5 daily missions.', 'Complete 5 daily missions.', 'SPECIAL', 'EASY', 'DAILY_MISSIONS_COMPLETED', 5, 75, 'check', FALSE, TRUE, 300),
    ('b1000007-0001-4000-8000-000000000031', 'MISSION_MASTER', 'Mission Master',
     'Complete 25 daily missions and master the daily mission loop.', 'Complete 25 daily missions.', 'SPECIAL', 'HARD', 'DAILY_MISSIONS_COMPLETED', 25, 250, 'check-circle', FALSE, TRUE, 310),
    ('b1000007-0001-4000-8000-000000000032', 'CHALLENGE_CHAMPION', 'Challenge Champion',
     'Complete 10 weekly challenges and become a challenge champion.', 'Complete 10 weekly challenges.', 'SPECIAL', 'EPIC', 'WEEKLY_CHALLENGES_COMPLETED', 10, 500, 'crown', FALSE, TRUE, 320)
ON CONFLICT (code) DO NOTHING;

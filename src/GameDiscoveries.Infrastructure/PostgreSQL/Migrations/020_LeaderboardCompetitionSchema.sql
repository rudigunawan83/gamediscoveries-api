-- Phase 10: Leaderboard & Competition

CREATE TABLE IF NOT EXISTS leaderboard_definitions (
    id              UUID PRIMARY KEY,
    code            VARCHAR(64) NOT NULL,
    name            VARCHAR(160) NOT NULL,
    description     TEXT NULL,
    type            VARCHAR(32) NOT NULL,
    score_type      VARCHAR(32) NOT NULL DEFAULT 'XP',
    period_type     VARCHAR(32) NOT NULL,
    scope_type      VARCHAR(32) NOT NULL DEFAULT 'GLOBAL',
    scope_value     VARCHAR(128) NULL,
    max_participants INTEGER NULL,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order      INTEGER NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_leaderboard_definitions_code UNIQUE (code),
    CONSTRAINT ck_leaderboard_definitions_type CHECK (type IN ('WEEKLY', 'MONTHLY', 'ALL_TIME', 'SEASONAL', 'EVENT', 'FRIENDS', 'CATEGORY', 'GENRE')),
    CONSTRAINT ck_leaderboard_definitions_score CHECK (score_type IN ('XP')),
    CONSTRAINT ck_leaderboard_definitions_period CHECK (period_type IN ('WEEKLY', 'MONTHLY', 'ALL_TIME', 'SEASONAL', 'EVENT')),
    CONSTRAINT ck_leaderboard_definitions_scope CHECK (scope_type IN ('GLOBAL', 'CATEGORY', 'GENRE', 'FRIENDS', 'EVENT'))
);

CREATE INDEX IF NOT EXISTS ix_leaderboard_definitions_active
    ON leaderboard_definitions (type, is_active, sort_order);

CREATE TABLE IF NOT EXISTS leaderboard_periods (
    id                  UUID PRIMARY KEY,
    code                VARCHAR(80) NOT NULL,
    type                VARCHAR(32) NOT NULL,
    name                VARCHAR(160) NOT NULL,
    description         TEXT NULL,
    start_at            TIMESTAMPTZ NOT NULL,
    end_at              TIMESTAMPTZ NOT NULL,
    timezone            VARCHAR(64) NOT NULL DEFAULT 'Asia/Jakarta',
    status              VARCHAR(32) NOT NULL DEFAULT 'SCHEDULED',
    score_type          VARCHAR(32) NOT NULL DEFAULT 'XP',
    reward_config_json  JSONB NULL,
    is_public           BOOLEAN NOT NULL DEFAULT TRUE,
    leaderboard_id      UUID NOT NULL REFERENCES leaderboard_definitions(id) ON DELETE CASCADE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_leaderboard_periods_code UNIQUE (code),
    CONSTRAINT ck_leaderboard_periods_type CHECK (type IN ('WEEKLY', 'MONTHLY', 'ALL_TIME', 'SEASONAL', 'EVENT')),
    CONSTRAINT ck_leaderboard_periods_status CHECK (status IN ('SCHEDULED', 'ACTIVE', 'END_PENDING', 'FINALIZING', 'FINALIZED', 'ENDED', 'CANCELLED', 'REWARDS_SETTLED')),
    CONSTRAINT ck_leaderboard_periods_range CHECK (end_at > start_at)
);

CREATE INDEX IF NOT EXISTS ix_leaderboard_periods_type_status
    ON leaderboard_periods (type, status);

CREATE INDEX IF NOT EXISTS ix_leaderboard_periods_window
    ON leaderboard_periods (leaderboard_id, start_at, end_at);

CREATE TABLE IF NOT EXISTS leaderboard_entries (
    id                  UUID PRIMARY KEY,
    leaderboard_id      UUID NOT NULL REFERENCES leaderboard_definitions(id) ON DELETE CASCADE,
    period_id           UUID NOT NULL REFERENCES leaderboard_periods(id) ON DELETE CASCADE,
    user_id             UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    score               BIGINT NOT NULL DEFAULT 0,
    rank                INTEGER NULL,
    previous_rank       INTEGER NULL,
    rank_change         INTEGER NOT NULL DEFAULT 0,
    games_played        INTEGER NOT NULL DEFAULT 0,
    valid_sessions      INTEGER NOT NULL DEFAULT 0,
    xp_earned           BIGINT NOT NULL DEFAULT 0,
    score_reached_at    TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    is_disqualified     BOOLEAN NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_leaderboard_entries_unique UNIQUE (leaderboard_id, period_id, user_id),
    CONSTRAINT ck_leaderboard_entries_score CHECK (score >= 0),
    CONSTRAINT ck_leaderboard_entries_xp CHECK (xp_earned >= 0)
);

CREATE INDEX IF NOT EXISTS ix_leaderboard_entries_rank
    ON leaderboard_entries (leaderboard_id, period_id, score DESC, score_reached_at ASC);

CREATE INDEX IF NOT EXISTS ix_leaderboard_entries_user
    ON leaderboard_entries (user_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS leaderboard_snapshots (
    id                  UUID PRIMARY KEY,
    leaderboard_id      UUID NOT NULL REFERENCES leaderboard_definitions(id) ON DELETE CASCADE,
    period_id           UUID NOT NULL REFERENCES leaderboard_periods(id) ON DELETE CASCADE,
    snapshot_at         TIMESTAMPTZ NOT NULL,
    total_participants  INTEGER NOT NULL DEFAULT 0,
    top_score           BIGINT NOT NULL DEFAULT 0,
    average_score       DOUBLE PRECISION NOT NULL DEFAULT 0,
    top_rank_data_json  JSONB NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_leaderboard_snapshots_period
    ON leaderboard_snapshots (period_id, snapshot_at DESC);

CREATE TABLE IF NOT EXISTS leaderboard_rank_history (
    id              UUID PRIMARY KEY,
    leaderboard_id  UUID NOT NULL REFERENCES leaderboard_definitions(id) ON DELETE CASCADE,
    period_id       UUID NOT NULL REFERENCES leaderboard_periods(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    rank            INTEGER NOT NULL,
    score           BIGINT NOT NULL,
    snapshot_at     TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_leaderboard_rank_history_user
    ON leaderboard_rank_history (user_id, snapshot_at DESC);

CREATE TABLE IF NOT EXISTS competitions (
    id                  UUID PRIMARY KEY,
    code                VARCHAR(80) NOT NULL,
    name                VARCHAR(160) NOT NULL,
    description         TEXT NULL,
    start_at            TIMESTAMPTZ NOT NULL,
    end_at              TIMESTAMPTZ NOT NULL,
    status              VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
    leaderboard_id      UUID NOT NULL REFERENCES leaderboard_definitions(id) ON DELETE CASCADE,
    period_id           UUID NULL REFERENCES leaderboard_periods(id) ON DELETE SET NULL,
    rules_json          JSONB NULL,
    reward_config_json  JSONB NULL,
    is_public           BOOLEAN NOT NULL DEFAULT TRUE,
    requires_join       BOOLEAN NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_competitions_code UNIQUE (code),
    CONSTRAINT ck_competitions_status CHECK (status IN ('DRAFT', 'SCHEDULED', 'ACTIVE', 'ENDED', 'CANCELLED', 'SETTLED')),
    CONSTRAINT ck_competitions_range CHECK (end_at > start_at)
);

CREATE INDEX IF NOT EXISTS ix_competitions_status
    ON competitions (status, start_at);

CREATE TABLE IF NOT EXISTS competition_participants (
    id              UUID PRIMARY KEY,
    competition_id  UUID NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    joined_at       TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    status          VARCHAR(32) NOT NULL DEFAULT 'ACTIVE',
    CONSTRAINT uq_competition_participants UNIQUE (competition_id, user_id),
    CONSTRAINT ck_competition_participants_status CHECK (status IN ('ACTIVE', 'DISQUALIFIED', 'WITHDRAWN'))
);

CREATE INDEX IF NOT EXISTS ix_competition_participants_user
    ON competition_participants (user_id, joined_at DESC);

CREATE TABLE IF NOT EXISTS competition_results (
    id                      UUID PRIMARY KEY,
    competition_id          UUID NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
    user_id                 UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    final_rank              INTEGER NOT NULL,
    final_score             BIGINT NOT NULL,
    reward_status           VARCHAR(32) NOT NULL DEFAULT 'PENDING',
    reward_transaction_id   UUID NULL,
    settled_at              TIMESTAMPTZ NULL,
    CONSTRAINT uq_competition_results UNIQUE (competition_id, user_id),
    CONSTRAINT ck_competition_results_reward CHECK (reward_status IN ('PENDING', 'AWARDED', 'FAILED', 'NOT_ELIGIBLE'))
);

CREATE INDEX IF NOT EXISTS ix_competition_results_rank
    ON competition_results (competition_id, final_rank);

-- Seed definitions
INSERT INTO leaderboard_definitions (id, code, name, description, type, score_type, period_type, scope_type, sort_order)
VALUES
    ('a1000000-0000-4000-8000-000000000001', 'GLOBAL_WEEKLY_XP', 'Weekly XP', 'Weekly XP competition leaderboard', 'WEEKLY', 'XP', 'WEEKLY', 'GLOBAL', 1),
    ('a1000000-0000-4000-8000-000000000002', 'GLOBAL_MONTHLY_XP', 'Monthly XP', 'Monthly XP competition leaderboard', 'MONTHLY', 'XP', 'MONTHLY', 'GLOBAL', 2),
    ('a1000000-0000-4000-8000-000000000003', 'GLOBAL_ALL_TIME_XP', 'All-Time XP', 'Lifetime eligible XP leaderboard', 'ALL_TIME', 'XP', 'ALL_TIME', 'GLOBAL', 3)
ON CONFLICT (code) DO NOTHING;

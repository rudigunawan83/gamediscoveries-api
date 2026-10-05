-- Phase 03: XP Engine

CREATE TABLE IF NOT EXISTS user_progress (
    user_id             UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    total_xp            BIGINT NOT NULL DEFAULT 0 CHECK (total_xp >= 0),
    level               INTEGER NOT NULL DEFAULT 1 CHECK (level >= 1),
    current_level_xp    BIGINT NOT NULL DEFAULT 0 CHECK (current_level_xp >= 0),
    current_streak      INTEGER NOT NULL DEFAULT 0 CHECK (current_streak >= 0),
    longest_streak      INTEGER NOT NULL DEFAULT 0 CHECK (longest_streak >= 0),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS xp_transactions (
    id                              UUID PRIMARY KEY,
    transaction_id                  UUID NOT NULL,
    user_id                         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    event_type                      VARCHAR(64) NOT NULL,
    reference_type                  VARCHAR(64) NOT NULL,
    reference_id                    VARCHAR(100) NOT NULL,
    rule_code                       VARCHAR(64) NOT NULL,
    xp_amount                       INTEGER NOT NULL,
    description                     VARCHAR(255) NOT NULL,
    metadata_json                   JSONB NOT NULL DEFAULT '{}'::jsonb,
    admin_id                        UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    reversal_of_transaction_id      UUID NULL REFERENCES xp_transactions(id) ON DELETE SET NULL,
    created_at                      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ux_xp_transactions_transaction_id UNIQUE (transaction_id),
    CONSTRAINT ux_xp_transactions_unique_reward UNIQUE (user_id, rule_code, reference_type, reference_id)
);

CREATE INDEX IF NOT EXISTS ix_xp_transactions_user_created
    ON xp_transactions (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_xp_transactions_user_rule_created
    ON xp_transactions (user_id, rule_code, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_xp_transactions_created
    ON xp_transactions (created_at DESC);

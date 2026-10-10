-- Rewarded video ads: one ticket per ad view, issued to a signed-in user and
-- redeemed only by a signature-verified AdMob server-side verification callback.
CREATE TABLE IF NOT EXISTS ad_reward_tickets (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token           VARCHAR(64) NOT NULL,
    platform        VARCHAR(20) NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    xp_awarded      INTEGER NOT NULL DEFAULT 0,
    reason          VARCHAR(50) NULL,
    ad_unit         VARCHAR(100) NULL,
    transaction_id  VARCHAR(128) NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    expires_at      TIMESTAMPTZ NOT NULL,
    completed_at    TIMESTAMPTZ NULL,
    CONSTRAINT ux_ad_reward_tickets_token UNIQUE (token),
    CONSTRAINT ux_ad_reward_tickets_transaction UNIQUE (transaction_id),
    CONSTRAINT ck_ad_reward_tickets_status CHECK (status IN ('PENDING', 'REWARDED', 'REJECTED'))
);

CREATE INDEX IF NOT EXISTS ix_ad_reward_tickets_user_created
    ON ad_reward_tickets (user_id, created_at DESC);

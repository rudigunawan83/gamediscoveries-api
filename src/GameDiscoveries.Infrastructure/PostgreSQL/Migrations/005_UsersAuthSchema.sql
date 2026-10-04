CREATE TABLE IF NOT EXISTS users (
    id              UUID PRIMARY KEY,
    email           VARCHAR(320) NOT NULL,
    password_hash   TEXT NOT NULL,
    display_name    VARCHAR(200) NULL,
    avatar_url      TEXT NULL,
    status          VARCHAR(50) NOT NULL DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_users_email UNIQUE (email)
);

CREATE TABLE IF NOT EXISTS user_roles (
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role            VARCHAR(50) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (user_id, role)
);

CREATE INDEX IF NOT EXISTS ix_users_status ON users (status);
CREATE INDEX IF NOT EXISTS ix_user_roles_role ON user_roles (role);

-- Bootstrap demo player for first login verification.
-- Password: GameDiscoveries!Demo1 (ASP.NET Identity V3 hash)
INSERT INTO users (id, email, password_hash, display_name, avatar_url, status)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'demo@gamediscoveries.com',
    'AQAAAAIAAYagAAAAEHEoPrNXY8HzLcgoDUVz2+RHOFA4cop8mOQpzt2ZklPQuVg1VP/FyH733q9DzGAbtA==',
    'Demo Player',
    NULL,
    'active'
)
ON CONFLICT (email) DO NOTHING;

INSERT INTO user_roles (user_id, role)
VALUES ('11111111-1111-1111-1111-111111111111', 'Player')
ON CONFLICT DO NOTHING;

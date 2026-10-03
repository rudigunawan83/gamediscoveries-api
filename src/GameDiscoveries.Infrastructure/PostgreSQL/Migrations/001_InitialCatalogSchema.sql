CREATE TABLE IF NOT EXISTS game_providers (
    id              UUID PRIMARY KEY,
    name            VARCHAR(100) NOT NULL UNIQUE,
    status          VARCHAR(50) NOT NULL DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS games (
    id              UUID PRIMARY KEY,
    slug            VARCHAR(200) NOT NULL UNIQUE,
    title           VARCHAR(300) NOT NULL,
    description     TEXT NULL,
    thumbnail_url   TEXT NULL,
    cover_url       TEXT NULL,
    game_url        TEXT NULL,
    status          VARCHAR(50) NOT NULL DEFAULT 'draft',
    mobile_ready    BOOLEAN NOT NULL DEFAULT FALSE,
    orientation     VARCHAR(50) NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    published_at    TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS categories (
    id              UUID PRIMARY KEY,
    slug            VARCHAR(150) NOT NULL UNIQUE,
    name            VARCHAR(200) NOT NULL,
    description     TEXT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS game_categories (
    game_id         UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    category_id     UUID NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (game_id, category_id)
);

CREATE TABLE IF NOT EXISTS game_tags (
    id              UUID PRIMARY KEY,
    game_id         UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    tag             VARCHAR(100) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_game_tags_game_tag UNIQUE (game_id, tag)
);

CREATE TABLE IF NOT EXISTS game_provider_mappings (
    id                  UUID PRIMARY KEY,
    game_id             UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    provider_id         UUID NOT NULL REFERENCES game_providers(id) ON DELETE CASCADE,
    provider_game_id    VARCHAR(200) NOT NULL,
    provider_url        TEXT NULL,
    raw_payload         JSONB NULL,
    last_synced_at      TIMESTAMPTZ NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_provider_game UNIQUE (provider_id, provider_game_id)
);

CREATE INDEX IF NOT EXISTS ix_games_status ON games (status);
CREATE INDEX IF NOT EXISTS ix_games_published_at ON games (published_at DESC NULLS LAST);
CREATE INDEX IF NOT EXISTS ix_games_mobile_ready ON games (mobile_ready) WHERE mobile_ready = TRUE;
CREATE INDEX IF NOT EXISTS ix_game_tags_tag ON game_tags (tag);
CREATE INDEX IF NOT EXISTS ix_game_provider_mappings_game_id ON game_provider_mappings (game_id);
CREATE INDEX IF NOT EXISTS ix_game_categories_category_id ON game_categories (category_id);

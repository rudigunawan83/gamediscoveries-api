-- Extend games for richer provider payloads without breaking existing rows.
ALTER TABLE games
    ADD COLUMN IF NOT EXISTS developer VARCHAR(200) NULL,
    ADD COLUMN IF NOT EXISTS embed_url TEXT NULL,
    ADD COLUMN IF NOT EXISTS width INTEGER NULL,
    ADD COLUMN IF NOT EXISTS height INTEGER NULL,
    ADD COLUMN IF NOT EXISTS platform VARCHAR(50) NOT NULL DEFAULT 'web';

CREATE TABLE IF NOT EXISTS platforms (
    id              UUID PRIMARY KEY,
    slug            VARCHAR(100) NOT NULL UNIQUE,
    name            VARCHAR(150) NOT NULL,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

INSERT INTO platforms (id, slug, name, is_active)
VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', 'web', 'Web', TRUE),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2', 'mobile', 'Mobile', TRUE),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3', 'desktop', 'Desktop', TRUE)
ON CONFLICT (slug) DO NOTHING;

-- Normalized tags (game_tags string rows remain for backward compatibility).
CREATE TABLE IF NOT EXISTS tags (
    id              UUID PRIMARY KEY,
    slug            VARCHAR(150) NOT NULL UNIQUE,
    name            VARCHAR(200) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS game_tag_links (
    game_id         UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    tag_id          UUID NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (game_id, tag_id)
);

CREATE TABLE IF NOT EXISTS game_feeds (
    id                  UUID PRIMARY KEY,
    name                VARCHAR(150) NOT NULL,
    slug                VARCHAR(150) NOT NULL UNIQUE,
    feed_type           VARCHAR(50) NOT NULL UNIQUE,
    source              VARCHAR(100) NOT NULL DEFAULT 'GameMonetize',
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    last_synced_at      TIMESTAMPTZ NULL,
    last_sync_status    VARCHAR(50) NULL,
    last_sync_error     TEXT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

INSERT INTO game_feeds (id, name, slug, feed_type, source, is_active)
VALUES
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'Latest', 'latest', 'Latest', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02', 'Popular', 'popular', 'Popular', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03', 'Action', 'action', 'Action', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb04', 'Puzzle', 'puzzle', 'Puzzle', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb05', 'Racing', 'racing', 'Racing', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb06', 'Sports', 'sports', 'Sports', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb07', 'Multiplayer', 'multiplayer', 'Multiplayer', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb08', 'Mobile', 'mobile', 'Mobile', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb09', 'Two Player', 'two-player', 'TwoPlayer', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb10', 'Featured', 'featured', 'Featured', 'GameMonetize', TRUE)
ON CONFLICT (feed_type) DO NOTHING;

CREATE TABLE IF NOT EXISTS game_feed_memberships (
    game_id         UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    feed_type       VARCHAR(50) NOT NULL,
    provider_id     UUID NOT NULL REFERENCES game_providers(id) ON DELETE CASCADE,
    synced_at       TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (game_id, feed_type)
);

CREATE INDEX IF NOT EXISTS ix_games_created_at ON games (created_at DESC);
CREATE INDEX IF NOT EXISTS ix_games_title_lower ON games (LOWER(title));
CREATE INDEX IF NOT EXISTS ix_games_platform ON games (platform);
CREATE INDEX IF NOT EXISTS ix_games_status_published ON games (status, published_at DESC NULLS LAST);
CREATE INDEX IF NOT EXISTS ix_game_feed_memberships_feed_type ON game_feed_memberships (feed_type, synced_at DESC);
CREATE INDEX IF NOT EXISTS ix_game_tag_links_tag_id ON game_tag_links (tag_id);
CREATE INDEX IF NOT EXISTS ix_tags_slug ON tags (slug);
CREATE INDEX IF NOT EXISTS ix_categories_name_lower ON categories (LOWER(name));

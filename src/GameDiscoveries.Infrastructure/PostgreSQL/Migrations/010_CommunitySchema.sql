-- Phase 11 Community schema

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS username VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS bio TEXT NULL,
    ADD COLUMN IF NOT EXISTS profile_visibility VARCHAR(20) NOT NULL DEFAULT 'public',
    ADD COLUMN IF NOT EXISTS show_favorites BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS show_history BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS show_achievements BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS show_activity BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS show_on_leaderboards BOOLEAN NOT NULL DEFAULT TRUE;

UPDATE users
SET username = LOWER(REGEXP_REPLACE(COALESCE(NULLIF(display_name, ''), SPLIT_PART(email, '@', 1)), '[^a-zA-Z0-9]+', '-', 'g'))
WHERE username IS NULL;

-- Ensure uniqueness for existing rows by appending short id suffix on collisions.
WITH ranked AS (
    SELECT id, username,
           ROW_NUMBER() OVER (PARTITION BY username ORDER BY created_at, id) AS rn
    FROM users
    WHERE username IS NOT NULL
)
UPDATE users u
SET username = LEFT(u.username, 40) || '-' || SUBSTRING(REPLACE(u.id::text, '-', ''), 1, 6)
FROM ranked r
WHERE u.id = r.id AND r.rn > 1;

ALTER TABLE users
    ALTER COLUMN username SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_users_username ON users (LOWER(username));

CREATE TABLE IF NOT EXISTS community_posts (
    id              UUID PRIMARY KEY,
    author_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id         UUID NULL REFERENCES games(id) ON DELETE SET NULL,
    type            VARCHAR(40) NOT NULL,
    title           VARCHAR(200) NOT NULL,
    content         TEXT NOT NULL,
    slug            VARCHAR(220) NOT NULL,
    status          VARCHAR(30) NOT NULL DEFAULT 'published',
    comment_count   INTEGER NOT NULL DEFAULT 0,
    reaction_count  INTEGER NOT NULL DEFAULT 0,
    view_count      INTEGER NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    deleted_at      TIMESTAMPTZ NULL,
    CONSTRAINT ck_community_posts_type CHECK (type IN ('discussion','recommendation','question','achievement_share','game_share')),
    CONSTRAINT ck_community_posts_status CHECK (status IN ('published','hidden','deleted'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_community_posts_slug ON community_posts (slug);
CREATE INDEX IF NOT EXISTS ix_community_posts_game_created ON community_posts (game_id, created_at DESC) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_community_posts_author_created ON community_posts (author_id, created_at DESC) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_community_posts_created ON community_posts (created_at DESC) WHERE deleted_at IS NULL AND status = 'published';

CREATE TABLE IF NOT EXISTS community_comments (
    id              UUID PRIMARY KEY,
    post_id         UUID NOT NULL REFERENCES community_posts(id) ON DELETE CASCADE,
    author_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    parent_id       UUID NULL REFERENCES community_comments(id) ON DELETE CASCADE,
    content         TEXT NOT NULL,
    status          VARCHAR(30) NOT NULL DEFAULT 'published',
    reaction_count  INTEGER NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    deleted_at      TIMESTAMPTZ NULL,
    CONSTRAINT ck_community_comments_status CHECK (status IN ('published','hidden','deleted'))
);

CREATE INDEX IF NOT EXISTS ix_community_comments_post_created ON community_comments (post_id, created_at ASC) WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS community_reactions (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    target_type     VARCHAR(30) NOT NULL,
    target_id       UUID NOT NULL,
    reaction        VARCHAR(30) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_community_reactions_user_target UNIQUE (user_id, target_type, target_id),
    CONSTRAINT ck_community_reactions_type CHECK (target_type IN ('post','comment','review')),
    CONSTRAINT ck_community_reactions_kind CHECK (reaction IN ('like','helpful','love','funny'))
);

CREATE INDEX IF NOT EXISTS ix_community_reactions_target ON community_reactions (target_type, target_id);

CREATE TABLE IF NOT EXISTS game_reviews (
    id              UUID PRIMARY KEY,
    game_id         UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    rating          SMALLINT NOT NULL,
    content         TEXT NOT NULL,
    status          VARCHAR(30) NOT NULL DEFAULT 'published',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    deleted_at      TIMESTAMPTZ NULL,
    CONSTRAINT uq_game_reviews_user_game UNIQUE (game_id, user_id),
    CONSTRAINT ck_game_reviews_rating CHECK (rating BETWEEN 1 AND 5),
    CONSTRAINT ck_game_reviews_status CHECK (status IN ('published','hidden','deleted'))
);

CREATE INDEX IF NOT EXISTS ix_game_reviews_game_created ON game_reviews (game_id, created_at DESC) WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS user_follows (
    follower_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    following_id    UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (follower_id, following_id),
    CONSTRAINT ck_user_follows_not_self CHECK (follower_id <> following_id)
);

CREATE INDEX IF NOT EXISTS ix_user_follows_following ON user_follows (following_id);

CREATE TABLE IF NOT EXISTS user_blocks (
    blocker_id      UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    blocked_id      UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (blocker_id, blocked_id),
    CONSTRAINT ck_user_blocks_not_self CHECK (blocker_id <> blocked_id)
);

CREATE TABLE IF NOT EXISTS achievements (
    id                  UUID PRIMARY KEY,
    code                VARCHAR(80) NOT NULL UNIQUE,
    name                VARCHAR(120) NOT NULL,
    description         TEXT NOT NULL,
    icon                VARCHAR(40) NOT NULL DEFAULT 'trophy',
    rarity              VARCHAR(30) NOT NULL DEFAULT 'common',
    requirement_type    VARCHAR(60) NOT NULL,
    requirement_value   INTEGER NOT NULL DEFAULT 1,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS user_achievements (
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    achievement_id  UUID NOT NULL REFERENCES achievements(id) ON DELETE CASCADE,
    unlocked_at     TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (user_id, achievement_id)
);

CREATE TABLE IF NOT EXISTS challenges (
    id                      UUID PRIMARY KEY,
    title                   VARCHAR(200) NOT NULL,
    description             TEXT NOT NULL,
    type                    VARCHAR(60) NOT NULL,
    target_value            INTEGER NOT NULL,
    start_at                TIMESTAMPTZ NOT NULL,
    end_at                  TIMESTAMPTZ NOT NULL,
    status                  VARCHAR(30) NOT NULL DEFAULT 'active',
    reward_achievement_id   UUID NULL REFERENCES achievements(id) ON DELETE SET NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ck_challenges_status CHECK (status IN ('draft','active','ended'))
);

CREATE TABLE IF NOT EXISTS user_challenges (
    challenge_id    UUID NOT NULL REFERENCES challenges(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    progress        INTEGER NOT NULL DEFAULT 0,
    completed_at    TIMESTAMPTZ NULL,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    PRIMARY KEY (challenge_id, user_id)
);

CREATE TABLE IF NOT EXISTS notifications (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type            VARCHAR(60) NOT NULL,
    actor_id        UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    entity_type     VARCHAR(40) NOT NULL,
    entity_id       UUID NOT NULL,
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb,
    read_at         TIMESTAMPTZ NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_notifications_user_created ON notifications (user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_notifications_user_unread ON notifications (user_id, created_at DESC) WHERE read_at IS NULL;

CREATE TABLE IF NOT EXISTS community_reports (
    id              UUID PRIMARY KEY,
    reporter_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    target_type     VARCHAR(30) NOT NULL,
    target_id       UUID NOT NULL,
    reason          VARCHAR(40) NOT NULL,
    description     TEXT NULL,
    status          VARCHAR(30) NOT NULL DEFAULT 'pending',
    moderator_id    UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    resolution      TEXT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    resolved_at     TIMESTAMPTZ NULL,
    CONSTRAINT ck_community_reports_status CHECK (status IN ('pending','reviewing','resolved','rejected'))
);

CREATE INDEX IF NOT EXISTS ix_community_reports_status_created ON community_reports (status, created_at DESC);

CREATE TABLE IF NOT EXISTS moderation_audit_logs (
    id              UUID PRIMARY KEY,
    actor_id        UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    action          VARCHAR(80) NOT NULL,
    target_type     VARCHAR(40) NOT NULL,
    target_id       UUID NOT NULL,
    details         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS community_activity (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    activity_type   VARCHAR(60) NOT NULL,
    game_id         UUID NULL REFERENCES games(id) ON DELETE SET NULL,
    entity_type     VARCHAR(40) NULL,
    entity_id       UUID NULL,
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_community_activity_created ON community_activity (created_at DESC);
CREATE INDEX IF NOT EXISTS ix_community_activity_user_created ON community_activity (user_id, created_at DESC);

INSERT INTO achievements (id, code, name, description, icon, rarity, requirement_type, requirement_value)
VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', 'first_game', 'First Game', 'Play your first game.', 'gamepad', 'common', 'games_played', 1),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02', 'first_favorite', 'First Favorite', 'Favorite a game.', 'heart', 'common', 'favorites', 1),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03', 'first_review', 'First Review', 'Write your first review.', 'star', 'common', 'reviews', 1),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04', 'first_post', 'First Community Post', 'Create a community post.', 'message', 'common', 'posts', 1),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05', 'first_comment', 'First Comment', 'Leave a community comment.', 'chat', 'common', 'comments', 1),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06', 'game_explorer', 'Game Explorer', 'Play 10 different games.', 'compass', 'rare', 'games_played', 10),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa07', 'community_contributor', 'Community Contributor', 'Create 5 posts.', 'users', 'rare', 'posts', 5),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa08', 'discovery_master', 'Discovery Master', 'Play 25 different games.', 'trophy', 'epic', 'games_played', 25)
ON CONFLICT (code) DO NOTHING;

INSERT INTO challenges (id, title, description, type, target_value, start_at, end_at, status, reward_achievement_id)
VALUES (
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01',
    'Discover 3 new games',
    'Play 3 different games this week.',
    'games_played',
    3,
    DATE_TRUNC('week', NOW() AT TIME ZONE 'utc'),
    DATE_TRUNC('week', NOW() AT TIME ZONE 'utc') + INTERVAL '7 days',
    'active',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06'
)
ON CONFLICT (id) DO NOTHING;

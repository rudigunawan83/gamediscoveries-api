-- Phase 09: Personalized Recommendation Engine

CREATE TABLE IF NOT EXISTS user_recommendation_profiles (
    id                      UUID PRIMARY KEY,
    user_id                 UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    preferred_genres_json   JSONB NOT NULL DEFAULT '{}'::jsonb,
    preferred_categories_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    preferred_tags_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    genre_scores_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    category_scores_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    tag_scores_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    preferred_game_ids_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    disliked_game_ids_json  JSONB NOT NULL DEFAULT '[]'::jsonb,
    total_interactions      INTEGER NOT NULL DEFAULT 0,
    profile_level           INTEGER NOT NULL DEFAULT 0,
    profile_version         INTEGER NOT NULL DEFAULT 1,
    last_calculated_at      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_user_recommendation_profiles_user UNIQUE (user_id),
    CONSTRAINT ck_user_recommendation_profiles_level CHECK (profile_level BETWEEN 0 AND 4),
    CONSTRAINT ck_user_recommendation_profiles_interactions CHECK (total_interactions >= 0)
);

CREATE INDEX IF NOT EXISTS ix_user_recommendation_profiles_updated
    ON user_recommendation_profiles (updated_at DESC);

CREATE TABLE IF NOT EXISTS recommendation_requests (
    id                      UUID PRIMARY KEY,
    user_id                 UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    anonymous_id            UUID NULL,
    strategy                VARCHAR(32) NOT NULL,
    section                 VARCHAR(64) NOT NULL,
    profile_level           INTEGER NOT NULL DEFAULT 0,
    candidate_count         INTEGER NOT NULL DEFAULT 0,
    result_count            INTEGER NOT NULL DEFAULT 0,
    algorithm_version       VARCHAR(64) NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_recommendation_requests_user_created
    ON recommendation_requests (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_recommendation_requests_anon_created
    ON recommendation_requests (anonymous_id, created_at DESC);

CREATE TABLE IF NOT EXISTS recommendation_impressions (
    id                          UUID PRIMARY KEY,
    recommendation_request_id   UUID NOT NULL REFERENCES recommendation_requests(id) ON DELETE CASCADE,
    user_id                     UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    anonymous_id                UUID NULL,
    game_id                     UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    section                     VARCHAR(64) NOT NULL,
    position                    INTEGER NOT NULL,
    strategy                    VARCHAR(32) NOT NULL,
    score                       DOUBLE PRECISION NOT NULL DEFAULT 0,
    reason_type                 VARCHAR(64) NULL,
    event_type                  VARCHAR(32) NOT NULL DEFAULT 'IMPRESSION',
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ck_recommendation_impressions_position CHECK (position > 0),
    CONSTRAINT ck_recommendation_impressions_event CHECK (
        event_type IN ('IMPRESSION', 'CLICK', 'GAME_START', 'FAVORITE', 'FEEDBACK')
    )
);

CREATE INDEX IF NOT EXISTS ix_recommendation_impressions_request
    ON recommendation_impressions (recommendation_request_id);

CREATE INDEX IF NOT EXISTS ix_recommendation_impressions_user_created
    ON recommendation_impressions (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_recommendation_impressions_game_created
    ON recommendation_impressions (game_id, created_at DESC);

CREATE TABLE IF NOT EXISTS recommendation_feedback (
    id                          UUID PRIMARY KEY,
    user_id                     UUID NULL REFERENCES users(id) ON DELETE CASCADE,
    anonymous_id                UUID NULL,
    game_id                     UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    recommendation_request_id   UUID NULL REFERENCES recommendation_requests(id) ON DELETE SET NULL,
    feedback_type               VARCHAR(32) NOT NULL,
    section                     VARCHAR(64) NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ck_recommendation_feedback_type CHECK (
        feedback_type IN ('LIKE', 'DISLIKE', 'NOT_INTERESTED', 'ALREADY_PLAYED', 'MORE_LIKE_THIS')
    )
);

CREATE INDEX IF NOT EXISTS ix_recommendation_feedback_user_game
    ON recommendation_feedback (user_id, game_id, created_at DESC);

CREATE TABLE IF NOT EXISTS recommendation_config (
    id                              UUID PRIMARY KEY,
    algorithm_version               VARCHAR(64) NOT NULL DEFAULT 'PERSONALIZED_V1',
    personal_relevance_weight       DOUBLE PRECISION NOT NULL DEFAULT 0.30,
    genre_weight                    DOUBLE PRECISION NOT NULL DEFAULT 0.15,
    category_weight                 DOUBLE PRECISION NOT NULL DEFAULT 0.10,
    tag_weight                      DOUBLE PRECISION NOT NULL DEFAULT 0.10,
    discovery_weight                DOUBLE PRECISION NOT NULL DEFAULT 0.10,
    trending_weight                 DOUBLE PRECISION NOT NULL DEFAULT 0.05,
    freshness_weight                DOUBLE PRECISION NOT NULL DEFAULT 0.05,
    novelty_weight                  DOUBLE PRECISION NOT NULL DEFAULT 0.05,
    engagement_weight               DOUBLE PRECISION NOT NULL DEFAULT 0.05,
    exploration_weight              DOUBLE PRECISION NOT NULL DEFAULT 0.05,
    candidate_pool_size             INTEGER NOT NULL DEFAULT 200,
    max_same_genre                  INTEGER NOT NULL DEFAULT 4,
    max_same_category               INTEGER NOT NULL DEFAULT 5,
    mmr_lambda                      DOUBLE PRECISION NOT NULL DEFAULT 0.80,
    cold_start_threshold            INTEGER NOT NULL DEFAULT 3,
    exploration_percentage          DOUBLE PRECISION NOT NULL DEFAULT 0.10,
    decay_days                      DOUBLE PRECISION NOT NULL DEFAULT 30,
    updated_at                      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by                      UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT ck_recommendation_config_weights CHECK (
        ABS((personal_relevance_weight + genre_weight + category_weight + tag_weight
            + discovery_weight + trending_weight + freshness_weight + novelty_weight
            + engagement_weight + exploration_weight) - 1.0) < 0.001
    )
);

INSERT INTO recommendation_config (id)
VALUES ('b1000009-0001-4000-8000-000000000001')
ON CONFLICT (id) DO NOTHING;

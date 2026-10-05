-- Phase 08: Discovery Score & Trending

CREATE TABLE IF NOT EXISTS discovery_score_config (
    id                              UUID PRIMARY KEY,
    score_version                   INTEGER NOT NULL DEFAULT 1,
    popularity_weight               DOUBLE PRECISION NOT NULL DEFAULT 0.20,
    engagement_weight               DOUBLE PRECISION NOT NULL DEFAULT 0.25,
    quality_weight                  DOUBLE PRECISION NOT NULL DEFAULT 0.15,
    momentum_weight                 DOUBLE PRECISION NOT NULL DEFAULT 0.20,
    growth_weight                   DOUBLE PRECISION NOT NULL DEFAULT 0.10,
    freshness_weight                DOUBLE PRECISION NOT NULL DEFAULT 0.10,
    trending_recent_weight          DOUBLE PRECISION NOT NULL DEFAULT 0.30,
    trending_momentum_weight        DOUBLE PRECISION NOT NULL DEFAULT 0.30,
    trending_growth_weight          DOUBLE PRECISION NOT NULL DEFAULT 0.20,
    trending_engagement_weight      DOUBLE PRECISION NOT NULL DEFAULT 0.15,
    trending_freshness_weight       DOUBLE PRECISION NOT NULL DEFAULT 0.05,
    freshness_decay_days            DOUBLE PRECISION NOT NULL DEFAULT 30,
    new_game_days                   INTEGER NOT NULL DEFAULT 14,
    min_valid_sessions_new_trending INTEGER NOT NULL DEFAULT 3,
    growth_smoothing                DOUBLE PRECISION NOT NULL DEFAULT 10,
    bayesian_m                      DOUBLE PRECISION NOT NULL DEFAULT 20,
    rising_growth_threshold         DOUBLE PRECISION NOT NULL DEFAULT 25,
    declining_growth_threshold      DOUBLE PRECISION NOT NULL DEFAULT -20,
    score_valid_minutes             INTEGER NOT NULL DEFAULT 90,
    updated_at                      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by                      UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT ck_discovery_score_config_version CHECK (score_version >= 1),
    CONSTRAINT ck_discovery_score_config_weights CHECK (
        ABS((popularity_weight + engagement_weight + quality_weight + momentum_weight + growth_weight + freshness_weight) - 1.0) < 0.001
    ),
    CONSTRAINT ck_discovery_score_config_trending_weights CHECK (
        ABS((trending_recent_weight + trending_momentum_weight + trending_growth_weight + trending_engagement_weight + trending_freshness_weight) - 1.0) < 0.001
    )
);

INSERT INTO discovery_score_config (id, score_version)
VALUES ('b1000008-0001-4000-8000-000000000001', 1)
ON CONFLICT (id) DO NOTHING;

CREATE TABLE IF NOT EXISTS game_discovery_metrics (
    id                      UUID PRIMARY KEY,
    game_id                 UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    window_start            TIMESTAMPTZ NOT NULL,
    window_end              TIMESTAMPTZ NOT NULL,
    window_type             VARCHAR(16) NOT NULL,
    views                   INTEGER NOT NULL DEFAULT 0,
    starts                  INTEGER NOT NULL DEFAULT 0,
    sessions                INTEGER NOT NULL DEFAULT 0,
    valid_sessions          INTEGER NOT NULL DEFAULT 0,
    active_seconds          BIGINT NOT NULL DEFAULT 0,
    unique_users            INTEGER NOT NULL DEFAULT 0,
    favorites               INTEGER NOT NULL DEFAULT 0,
    ratings                 INTEGER NOT NULL DEFAULT 0,
    reviews                 INTEGER NOT NULL DEFAULT 0,
    shares                  INTEGER NOT NULL DEFAULT 0,
    returning_users         INTEGER NOT NULL DEFAULT 0,
    avg_session_seconds     DOUBLE PRECISION NOT NULL DEFAULT 0,
    avg_sessions_per_user   DOUBLE PRECISION NOT NULL DEFAULT 0,
    favorite_rate           DOUBLE PRECISION NOT NULL DEFAULT 0,
    rating_rate             DOUBLE PRECISION NOT NULL DEFAULT 0,
    review_rate             DOUBLE PRECISION NOT NULL DEFAULT 0,
    share_rate              DOUBLE PRECISION NOT NULL DEFAULT 0,
    engagement_rate         DOUBLE PRECISION NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_game_discovery_metrics UNIQUE (game_id, window_start, window_type),
    CONSTRAINT ck_game_discovery_metrics_window CHECK (
        window_type IN ('HOURLY', 'DAILY', 'WEEKLY', 'MONTHLY')
    ),
    CONSTRAINT ck_game_discovery_metrics_nonneg CHECK (
        views >= 0 AND starts >= 0 AND sessions >= 0 AND valid_sessions >= 0
        AND active_seconds >= 0 AND unique_users >= 0 AND favorites >= 0
        AND ratings >= 0 AND reviews >= 0 AND shares >= 0 AND returning_users >= 0
    )
);

CREATE INDEX IF NOT EXISTS ix_game_discovery_metrics_game_window
    ON game_discovery_metrics (game_id, window_type, window_start DESC);

CREATE INDEX IF NOT EXISTS ix_game_discovery_metrics_window
    ON game_discovery_metrics (window_type, window_start DESC);

CREATE TABLE IF NOT EXISTS game_discovery_scores (
    id                      UUID PRIMARY KEY,
    game_id                 UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    score                   DOUBLE PRECISION NOT NULL DEFAULT 0,
    popularity_score        DOUBLE PRECISION NOT NULL DEFAULT 0,
    engagement_score        DOUBLE PRECISION NOT NULL DEFAULT 0,
    quality_score           DOUBLE PRECISION NOT NULL DEFAULT 0,
    momentum_score          DOUBLE PRECISION NOT NULL DEFAULT 0,
    growth_score            DOUBLE PRECISION NOT NULL DEFAULT 0,
    freshness_score         DOUBLE PRECISION NOT NULL DEFAULT 0,
    trending_score          DOUBLE PRECISION NOT NULL DEFAULT 0,
    trend_state             VARCHAR(16) NOT NULL DEFAULT 'STABLE',
    trend_percentage        DOUBLE PRECISION NOT NULL DEFAULT 0,
    score_version           INTEGER NOT NULL DEFAULT 1,
    calculated_at           TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    valid_until             TIMESTAMPTZ NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT uq_game_discovery_scores_game UNIQUE (game_id),
    CONSTRAINT ck_game_discovery_scores_trend CHECK (
        trend_state IN ('RISING', 'HOT', 'STABLE', 'DECLINING', 'NEW')
    ),
    CONSTRAINT ck_game_discovery_scores_range CHECK (
        score BETWEEN 0 AND 100
        AND popularity_score BETWEEN 0 AND 100
        AND engagement_score BETWEEN 0 AND 100
        AND quality_score BETWEEN 0 AND 100
        AND momentum_score BETWEEN 0 AND 100
        AND growth_score BETWEEN 0 AND 100
        AND freshness_score BETWEEN 0 AND 100
        AND trending_score BETWEEN 0 AND 100
    )
);

CREATE INDEX IF NOT EXISTS ix_game_discovery_scores_score
    ON game_discovery_scores (score DESC, trending_score DESC);

CREATE INDEX IF NOT EXISTS ix_game_discovery_scores_trending
    ON game_discovery_scores (trending_score DESC, calculated_at DESC);

CREATE TABLE IF NOT EXISTS game_discovery_score_history (
    id                      UUID PRIMARY KEY,
    game_id                 UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    discovery_score         DOUBLE PRECISION NOT NULL,
    popularity_score        DOUBLE PRECISION NOT NULL,
    engagement_score        DOUBLE PRECISION NOT NULL,
    quality_score           DOUBLE PRECISION NOT NULL,
    momentum_score          DOUBLE PRECISION NOT NULL,
    growth_score            DOUBLE PRECISION NOT NULL,
    freshness_score         DOUBLE PRECISION NOT NULL,
    trending_score          DOUBLE PRECISION NOT NULL,
    trend_state             VARCHAR(16) NOT NULL,
    trend_percentage        DOUBLE PRECISION NOT NULL DEFAULT 0,
    score_version           INTEGER NOT NULL,
    calculated_at           TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS ix_game_discovery_score_history_game
    ON game_discovery_score_history (game_id, calculated_at DESC);

CREATE TABLE IF NOT EXISTS game_trending_snapshots (
    id                      UUID PRIMARY KEY,
    game_id                 UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    ranking_type            VARCHAR(32) NOT NULL,
    period_type             VARCHAR(16) NOT NULL,
    score                   DOUBLE PRECISION NOT NULL DEFAULT 0,
    rank                    INTEGER NOT NULL,
    previous_rank           INTEGER NULL,
    rank_change             INTEGER NOT NULL DEFAULT 0,
    trend_state             VARCHAR(16) NOT NULL DEFAULT 'STABLE',
    trend_percentage        DOUBLE PRECISION NOT NULL DEFAULT 0,
    score_version           INTEGER NOT NULL DEFAULT 1,
    calculated_at           TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT ck_game_trending_snapshots_type CHECK (
        ranking_type IN (
            'TRENDING', 'RISING', 'POPULAR', 'MOST_PLAYED', 'MOST_FAVORITED',
            'MOST_RATED', 'MOST_REVIEWED', 'NEW_TRENDING'
        )
    ),
    CONSTRAINT ck_game_trending_snapshots_period CHECK (
        period_type IN ('HOUR', 'DAY', 'WEEK')
    ),
    CONSTRAINT ck_game_trending_snapshots_rank CHECK (rank > 0)
);

CREATE INDEX IF NOT EXISTS ix_game_trending_snapshots_lookup
    ON game_trending_snapshots (ranking_type, period_type, calculated_at DESC, rank);

CREATE INDEX IF NOT EXISTS ix_game_trending_snapshots_game
    ON game_trending_snapshots (game_id, calculated_at DESC);

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_game_ended
    ON game_play_sessions (game_id, ended_at DESC)
    WHERE ended_at IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_game_play_sessions_game_valid_ended
    ON game_play_sessions (game_id, is_valid, ended_at DESC)
    WHERE ended_at IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_analytics_events_game_type_occurred
    ON analytics_events (game_id, event_name, occurred_at DESC)
    WHERE game_id IS NOT NULL;

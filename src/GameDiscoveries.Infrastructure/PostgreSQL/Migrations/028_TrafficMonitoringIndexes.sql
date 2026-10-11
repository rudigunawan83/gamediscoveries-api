-- Bound traffic monitoring scans to the selected time window.
CREATE INDEX IF NOT EXISTS ix_analytics_events_occurred_at
    ON analytics_events (occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_analytics_sessions_started_at
    ON analytics_sessions (started_at DESC);

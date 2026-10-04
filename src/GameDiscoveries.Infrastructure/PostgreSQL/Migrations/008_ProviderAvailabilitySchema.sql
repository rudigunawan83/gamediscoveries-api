-- Provider availability tracking for sync reconcile (Phase 04).
ALTER TABLE game_provider_mappings
    ADD COLUMN IF NOT EXISTS last_seen_at TIMESTAMPTZ NULL;

ALTER TABLE game_provider_mappings
    ADD COLUMN IF NOT EXISTS availability_status VARCHAR(50) NOT NULL DEFAULT 'active';

UPDATE game_provider_mappings
SET last_seen_at = COALESCE(last_seen_at, last_synced_at, updated_at, created_at)
WHERE last_seen_at IS NULL;

UPDATE game_provider_mappings
SET availability_status = 'active'
WHERE availability_status IS NULL OR BTRIM(availability_status) = '';

ALTER TABLE game_provider_mappings
    DROP CONSTRAINT IF EXISTS ck_game_provider_mappings_availability;

ALTER TABLE game_provider_mappings
    ADD CONSTRAINT ck_game_provider_mappings_availability
    CHECK (availability_status IN ('active', 'unavailable', 'pending', 'blocked', 'inactive'));

CREATE INDEX IF NOT EXISTS ix_game_provider_mappings_availability
    ON game_provider_mappings (provider_id, availability_status);

CREATE INDEX IF NOT EXISTS ix_game_provider_mappings_last_seen
    ON game_provider_mappings (provider_id, last_seen_at DESC NULLS LAST);

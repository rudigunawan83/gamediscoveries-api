-- Preserve provider feed order so each discovery shelf follows its own ranking.
ALTER TABLE game_feed_memberships
    ADD COLUMN IF NOT EXISTS position INTEGER NULL;

CREATE INDEX IF NOT EXISTS ix_game_feed_memberships_feed_position
    ON game_feed_memberships (feed_type, position);

-- Restore games archived by the Latest sync while still listed in another live feed.
WITH restored AS (
    UPDATE game_provider_mappings m
    SET availability_status = 'active',
        updated_at = NOW()
    WHERE m.availability_status = 'unavailable'
      AND EXISTS (
          SELECT 1
          FROM game_feed_memberships fm
          WHERE fm.game_id = m.game_id
            AND fm.feed_type <> 'Latest'
            AND fm.synced_at >= NOW() - INTERVAL '2 days')
    RETURNING m.game_id
)
UPDATE games g
SET status = 'published',
    updated_at = NOW()
FROM restored r
WHERE g.id = r.game_id
  AND g.status = 'archived';

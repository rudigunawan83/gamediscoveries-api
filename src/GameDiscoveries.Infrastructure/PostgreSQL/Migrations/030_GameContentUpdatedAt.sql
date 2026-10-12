-- updated_at moves on every feed sync; sitemap lastmod needs the time the page content last changed.
ALTER TABLE games
    ADD COLUMN IF NOT EXISTS content_updated_at TIMESTAMPTZ NULL;

UPDATE games
SET content_updated_at = COALESCE(published_at, created_at)
WHERE content_updated_at IS NULL;

ALTER TABLE games
    ALTER COLUMN content_updated_at SET DEFAULT NOW(),
    ALTER COLUMN content_updated_at SET NOT NULL;

-- Duplicate-title lookups for canonical selection.
CREATE INDEX IF NOT EXISTS ix_games_published_title_lower
    ON games (lower(title))
    WHERE status = 'published';

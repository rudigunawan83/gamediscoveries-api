-- UI language chosen by the user; SYSTEM means "follow the device/browser language".
-- Only the choice is stored here, translations live in the clients.
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS preferred_language VARCHAR(10) NOT NULL DEFAULT 'SYSTEM';

ALTER TABLE users DROP CONSTRAINT IF EXISTS ck_users_preferred_language;
ALTER TABLE users
    ADD CONSTRAINT ck_users_preferred_language CHECK (preferred_language IN ('SYSTEM', 'en', 'id'));

-- Bootstrap superadmin account for full admin-area access.
-- Email: superadmin@gamediscoveries.com
-- Password: GameDiscoveries!SuperAdmin1
INSERT INTO users (
    id, email, password_hash, display_name, avatar_url, status,
    username, bio, profile_visibility, show_favorites, show_history,
    show_achievements, show_activity, show_on_leaderboards)
VALUES (
    '99999999-9999-9999-9999-999999999999',
    'superadmin@gamediscoveries.com',
    'AQAAAAIAAYagAAAAEBcMuBtzBWaHDEcwtpiQSGvaZO9brGlRlkp2EHI0By1+KRAWyM4bNtBHWziyfIqBlQ==',
    'Super Admin',
    NULL,
    'active',
    'superadmin',
    'GameDiscoveries super administrator.',
    'public',
    TRUE,
    TRUE,
    TRUE,
    TRUE,
    TRUE
)
ON CONFLICT (email) DO UPDATE
SET
    password_hash = EXCLUDED.password_hash,
    display_name = EXCLUDED.display_name,
    username = EXCLUDED.username,
    bio = EXCLUDED.bio,
    profile_visibility = EXCLUDED.profile_visibility,
    show_favorites = EXCLUDED.show_favorites,
    show_history = EXCLUDED.show_history,
    show_achievements = EXCLUDED.show_achievements,
    show_activity = EXCLUDED.show_activity,
    show_on_leaderboards = EXCLUDED.show_on_leaderboards,
    status = 'active',
    updated_at = NOW() AT TIME ZONE 'utc';

INSERT INTO user_roles (user_id, role)
VALUES
    ('99999999-9999-9999-9999-999999999999', 'Player'),
    ('99999999-9999-9999-9999-999999999999', 'Admin'),
    ('99999999-9999-9999-9999-999999999999', 'SuperAdmin')
ON CONFLICT DO NOTHING;

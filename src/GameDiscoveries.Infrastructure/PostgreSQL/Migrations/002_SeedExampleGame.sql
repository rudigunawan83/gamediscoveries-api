INSERT INTO game_providers (id, name, status, created_at, updated_at)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'GameMonetize',
    'active',
    NOW() AT TIME ZONE 'utc',
    NOW() AT TIME ZONE 'utc'
)
ON CONFLICT (name) DO NOTHING;

INSERT INTO games (
    id,
    slug,
    title,
    description,
    thumbnail_url,
    cover_url,
    game_url,
    status,
    mobile_ready,
    orientation,
    created_at,
    updated_at,
    published_at
)
VALUES (
    '22222222-2222-2222-2222-222222222222',
    'example-game',
    'Example Game',
    'A sample game used to validate the GameDiscoveries catalog architecture.',
    'https://cdn.gamediscoveries.com/games/example-game/thumb.jpg',
    'https://cdn.gamediscoveries.com/games/example-game/cover.jpg',
    'https://cdn.gamediscoveries.com/games/example-game/play',
    'published',
    TRUE,
    'landscape',
    NOW() AT TIME ZONE 'utc',
    NOW() AT TIME ZONE 'utc',
    NOW() AT TIME ZONE 'utc'
)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO categories (id, slug, name, description, created_at, updated_at)
VALUES (
    '33333333-3333-3333-3333-333333333333',
    'racing',
    'Racing',
    'High-speed racing games',
    NOW() AT TIME ZONE 'utc',
    NOW() AT TIME ZONE 'utc'
)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO game_categories (game_id, category_id, created_at)
VALUES (
    '22222222-2222-2222-2222-222222222222',
    '33333333-3333-3333-3333-333333333333',
    NOW() AT TIME ZONE 'utc'
)
ON CONFLICT DO NOTHING;

INSERT INTO game_tags (id, game_id, tag, created_at)
VALUES (
    '44444444-4444-4444-4444-444444444444',
    '22222222-2222-2222-2222-222222222222',
    'arcade',
    NOW() AT TIME ZONE 'utc'
)
ON CONFLICT DO NOTHING;

INSERT INTO game_provider_mappings (
    id,
    game_id,
    provider_id,
    provider_game_id,
    provider_url,
    raw_payload,
    last_synced_at,
    created_at,
    updated_at
)
VALUES (
    '55555555-5555-5555-5555-555555555555',
    '22222222-2222-2222-2222-222222222222',
    '11111111-1111-1111-1111-111111111111',
    'gm-example-001',
    'https://gamemonetize.com/games/example',
    '{"title":"Example Game"}'::jsonb,
    NOW() AT TIME ZONE 'utc',
    NOW() AT TIME ZONE 'utc',
    NOW() AT TIME ZONE 'utc'
)
ON CONFLICT DO NOTHING;

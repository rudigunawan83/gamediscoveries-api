INSERT INTO game_feeds (id, name, slug, feed_type, source, is_active)
VALUES
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb11', 'Hot Games', 'hot-games', 'HotGames', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb12', 'Best Games', 'best-games', 'BestGames', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb13', 'Most Played', 'most-played', 'MostPlayed', 'GameMonetize', TRUE),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb14', 'Exclusive Games', 'exclusive-games', 'ExclusiveGames', 'GameMonetize', TRUE)
ON CONFLICT (feed_type) DO NOTHING;

-- Additional demo data for missions, progress, leaderboards, and community.
-- All seeded users use password: GameDiscoveries!Demo1

DO $$
DECLARE
    seed_user RECORD;
    jakarta_now TIMESTAMP;
    day_start TIMESTAMPTZ;
    day_end TIMESTAMPTZ;
    week_start TIMESTAMPTZ;
    week_end TIMESTAMPTZ;
    month_start TIMESTAMPTZ;
    month_end TIMESTAMPTZ;
    weekly_code TEXT;
    monthly_code TEXT;
    all_time_code TEXT := 'ALL_TIME';
    password_hash TEXT := 'AQAAAAIAAYagAAAAEHEoPrNXY8HzLcgoDUVz2+RHOFA4cop8mOQpzt2ZklPQuVg1VP/FyH733q9DzGAbtA==';
    demo_user_id UUID;
    seeded_user_id UUID;
    target_post_id UUID;
    next_user_id UUID;
    post_id UUID;
    comment_id UUID;
    reaction_id UUID;
BEGIN
    jakarta_now := timezone('Asia/Jakarta', NOW());
    day_start := date_trunc('day', jakarta_now) AT TIME ZONE 'Asia/Jakarta';
    day_end := (date_trunc('day', jakarta_now) + INTERVAL '1 day') AT TIME ZONE 'Asia/Jakarta';
    week_start := date_trunc('week', jakarta_now) AT TIME ZONE 'Asia/Jakarta';
    week_end := (date_trunc('week', jakarta_now) + INTERVAL '7 days') AT TIME ZONE 'Asia/Jakarta';
    month_start := date_trunc('month', jakarta_now) AT TIME ZONE 'Asia/Jakarta';
    month_end := (date_trunc('month', jakarta_now) + INTERVAL '1 month') AT TIME ZONE 'Asia/Jakarta';
    weekly_code := 'WEEKLY_' || to_char(date_trunc('week', jakarta_now), 'IYYY-"W"IW');
    monthly_code := 'MONTHLY_' || to_char(jakarta_now, 'YYYY-MM');

    SELECT id INTO demo_user_id
    FROM users
    WHERE LOWER(email) = 'demo@gamediscoveries.com'
    LIMIT 1;

    INSERT INTO leaderboard_periods (
        id, code, type, name, description, start_at, end_at, timezone, status,
        score_type, is_public, leaderboard_id, created_at, updated_at)
    SELECT gen_random_uuid(), weekly_code, 'WEEKLY', 'Weekly XP (' || weekly_code || ')',
           'Weekly XP competition leaderboard', week_start, week_end, 'Asia/Jakarta', 'ACTIVE',
           'XP', TRUE, ld.id, NOW(), NOW()
    FROM leaderboard_definitions ld
    WHERE ld.code = 'GLOBAL_WEEKLY_XP'
    ON CONFLICT (code) DO UPDATE SET status = 'ACTIVE', updated_at = NOW();

    INSERT INTO leaderboard_periods (
        id, code, type, name, description, start_at, end_at, timezone, status,
        score_type, is_public, leaderboard_id, created_at, updated_at)
    SELECT gen_random_uuid(), monthly_code, 'MONTHLY', 'Monthly XP (' || monthly_code || ')',
           'Monthly XP competition leaderboard', month_start, month_end, 'Asia/Jakarta', 'ACTIVE',
           'XP', TRUE, ld.id, NOW(), NOW()
    FROM leaderboard_definitions ld
    WHERE ld.code = 'GLOBAL_MONTHLY_XP'
    ON CONFLICT (code) DO UPDATE SET status = 'ACTIVE', updated_at = NOW();

    INSERT INTO leaderboard_periods (
        id, code, type, name, description, start_at, end_at, timezone, status,
        score_type, is_public, leaderboard_id, created_at, updated_at)
    SELECT gen_random_uuid(), all_time_code, 'ALL_TIME', 'All-Time XP',
           'Lifetime eligible XP leaderboard', '2020-01-01 00:00:00+00'::timestamptz,
           '2099-12-31 23:59:59+00'::timestamptz, 'Asia/Jakarta', 'ACTIVE',
           'XP', TRUE, ld.id, NOW(), NOW()
    FROM leaderboard_definitions ld
    WHERE ld.code = 'GLOBAL_ALL_TIME_XP'
    ON CONFLICT (code) DO UPDATE SET status = 'ACTIVE', updated_at = NOW();

    FOR seed_user IN
        SELECT *
        FROM (VALUES
            (1,  'player01@gamediscoveries.com', 'Ayu Explorer',   'player01', 'Puzzle and casual game explorer.',      5200, 14, 600, 52, 18, 30, 12, 16, 4,  'discussion',        'Tips mengejar XP mingguan',       'Aku fokus menyelesaikan mission harian sebelum mencoba game baru.'),
            (2,  'player02@gamediscoveries.com', 'Bima Runner',    'player02', 'Arcade runner and score chaser.',       4875, 13, 475, 48, 16, 27, 11, 14, 5,  'recommendation',    'Rekomendasi game cepat untuk break', 'Game pendek sangat membantu menjaga streak tanpa terasa berat.'),
            (3,  'player03@gamediscoveries.com', 'Citra Quest',    'player03', 'Quest lover and mission finisher.',      4620, 13, 220, 44, 15, 25, 10, 13, 6,  'question',          'Genre apa yang paling cepat naik XP?', 'Aku ingin mencoba genre baru minggu ini, ada saran?'),
            (4,  'player04@gamediscoveries.com', 'Danu Blitz',     'player04', 'Fast paced HTML5 game fan.',             4300, 12, 500, 41, 13, 23, 9,  12, 7,  'achievement_share','Akhirnya unlock streak 7 hari',   'Streak kecil ternyata bikin progres terasa konsisten.'),
            (5,  'player05@gamediscoveries.com', 'Eka Puzzle',     'player05', 'Puzzle specialist.',                     3980, 12, 180, 38, 12, 22, 8,  10, 8,  'game_share',       'Game puzzle favorit minggu ini',  'Aku suka game yang bisa selesai dalam beberapa menit.'),
            (6,  'player06@gamediscoveries.com', 'Fajar Nova',     'player06', 'Explores new releases daily.',           3650, 11, 350, 35, 11, 20, 7,  9,  9,  'discussion',       'Cara pilih game baru',            'Aku biasanya mulai dari kategori trending lalu lanjut ke rekomendasi.'),
            (7,  'player07@gamediscoveries.com', 'Gita Spark',     'player07', 'Achievement hunter.',                    3320, 11, 20,  32, 10, 18, 6,  8,  10, 'recommendation',   'Game santai untuk menjaga streak','Kalau sedang sibuk, game casual pendek paling cocok.'),
            (8,  'player08@gamediscoveries.com', 'Hadi Pixel',     'player08', 'Retro and pixel game fan.',              3050, 10, 250, 29, 9,  17, 5,  7,  11, 'question',         'Ada yang suka game retro?',       'Aku cari game pixel ringan untuk dimainkan malam ini.'),
            (9,  'player09@gamediscoveries.com', 'Intan Drift',    'player09', 'Racing and action player.',              2780, 10, 80,  26, 8,  15, 4,  6,  12, 'achievement_share','Naik level hari ini',             'Mission mingguan lumayan membantu naik level.'),
            (10, 'player10@gamediscoveries.com', 'Joko Orbit',     'player10', 'New game discovery enthusiast.',         2510, 9,  310, 23, 7,  14, 3,  5,  13, 'game_share',       'Game discovery pertama minggu ini','Aku baru mulai ikut leaderboard dan targetku Top 10.')
        ) AS s(idx, email, display_name, username, bio, score, level, current_level_xp, games_played, favorites_count, unique_games_played, current_streak, longest_streak, previous_rank, post_type, post_title, post_content)
    LOOP
        seeded_user_id := ('22222222-2222-4222-8222-' || LPAD(seed_user.idx::text, 12, '0'))::uuid;
        post_id := ('c2200000-0000-4000-8000-' || LPAD(seed_user.idx::text, 12, '0'))::uuid;
        comment_id := ('c2210000-0000-4000-8000-' || LPAD(seed_user.idx::text, 12, '0'))::uuid;
        reaction_id := ('c2220000-0000-4000-8000-' || LPAD(seed_user.idx::text, 12, '0'))::uuid;
        target_post_id := ('c2200000-0000-4000-8000-' || LPAD((CASE WHEN seed_user.idx = 1 THEN seed_user.idx ELSE seed_user.idx - 1 END)::text, 12, '0'))::uuid;
        next_user_id := ('22222222-2222-4222-8222-' || LPAD((CASE WHEN seed_user.idx = 10 THEN 1 ELSE seed_user.idx + 1 END)::text, 12, '0'))::uuid;

        INSERT INTO users (
            id, email, password_hash, display_name, avatar_url, status,
            username, bio, profile_visibility, show_favorites, show_history,
            show_achievements, show_activity, show_on_leaderboards, created_at, updated_at)
        VALUES (
            seeded_user_id, seed_user.email, password_hash, seed_user.display_name, NULL, 'active',
            seed_user.username, seed_user.bio, 'public', TRUE, TRUE,
            TRUE, TRUE, TRUE, NOW() - (seed_user.idx || ' days')::interval, NOW())
        ON CONFLICT (email) DO UPDATE SET
            password_hash = EXCLUDED.password_hash,
            display_name = EXCLUDED.display_name,
            username = EXCLUDED.username,
            bio = EXCLUDED.bio,
            status = 'active',
            show_on_leaderboards = TRUE,
            updated_at = NOW();

        INSERT INTO user_roles (user_id, role)
        VALUES (seeded_user_id, 'Player')
        ON CONFLICT DO NOTHING;

        INSERT INTO user_progress (
            user_id, total_xp, level, current_level_xp, current_streak, longest_streak,
            games_played, favorites_count, unique_games_played, last_activity_at,
            streak_start_date, last_qualifying_activity_date, streak_status,
            streak_freeze_count, streak_updated_at, created_at, updated_at)
        VALUES (
            seeded_user_id, seed_user.score, seed_user.level, seed_user.current_level_xp,
            seed_user.current_streak, seed_user.longest_streak, seed_user.games_played,
            seed_user.favorites_count, seed_user.unique_games_played, NOW(),
            (jakarta_now::date - seed_user.current_streak + 1), jakarta_now::date,
            'ACTIVE', 1, NOW(), NOW(), NOW())
        ON CONFLICT (user_id) DO UPDATE SET
            total_xp = GREATEST(user_progress.total_xp, EXCLUDED.total_xp),
            level = GREATEST(user_progress.level, EXCLUDED.level),
            current_level_xp = GREATEST(user_progress.current_level_xp, EXCLUDED.current_level_xp),
            current_streak = GREATEST(user_progress.current_streak, EXCLUDED.current_streak),
            longest_streak = GREATEST(user_progress.longest_streak, EXCLUDED.longest_streak),
            games_played = GREATEST(user_progress.games_played, EXCLUDED.games_played),
            favorites_count = GREATEST(user_progress.favorites_count, EXCLUDED.favorites_count),
            unique_games_played = GREATEST(user_progress.unique_games_played, EXCLUDED.unique_games_played),
            last_activity_at = NOW(),
            last_qualifying_activity_date = jakarta_now::date,
            streak_status = 'ACTIVE',
            updated_at = NOW();

        INSERT INTO xp_transactions (
            id, transaction_id, user_id, event_type, reference_type, reference_id,
            rule_code, xp_amount, description, metadata_json, created_at)
        VALUES (
            gen_random_uuid(),
            ('e2200000-0000-4000-8000-' || LPAD(seed_user.idx::text, 12, '0'))::uuid,
            seeded_user_id,
            'GAME_SESSION_END',
            'DEMO_SEED',
            'phase22-score-' || LPAD(seed_user.idx::text, 2, '0'),
            'DEMO_LEADERBOARD_SCORE',
            seed_user.score,
            'Phase 22 demo leaderboard score',
            jsonb_build_object('seed', 'phase22', 'source', 'demo-community-leaderboard'),
            NOW() - ((12 - seed_user.idx) || ' hours')::interval)
        ON CONFLICT (transaction_id) DO NOTHING;

        INSERT INTO user_missions (
            id, user_id, mission_template_id, code, type, title, description,
            requirement_type, period_start, period_end, progress_value, target_value,
            reward_xp, status, completed_at, created_at, updated_at)
        SELECT
            gen_random_uuid(), seeded_user_id, mt.id, mt.code, mt.type, mt.title, mt.description,
            mt.requirement_type,
            CASE WHEN mt.type = 'DAILY' THEN day_start ELSE week_start END,
            CASE WHEN mt.type = 'DAILY' THEN day_end ELSE week_end END,
            mt.target_value, mt.target_value, mt.reward_xp, 'COMPLETED',
            NOW() - (seed_user.idx || ' hours')::interval, NOW(), NOW()
        FROM mission_templates mt
        WHERE mt.code IN ('PLAY_2_GAMES', 'FAVORITE_A_GAME', 'PLAY_5_GAMES')
        ON CONFLICT (user_id, mission_template_id, period_start) DO UPDATE SET
            progress_value = EXCLUDED.progress_value,
            status = 'COMPLETED',
            completed_at = COALESCE(user_missions.completed_at, EXCLUDED.completed_at),
            updated_at = NOW();

        INSERT INTO user_missions (
            id, user_id, mission_template_id, code, type, title, description,
            requirement_type, period_start, period_end, progress_value, target_value,
            reward_xp, status, completed_at, created_at, updated_at)
        SELECT
            gen_random_uuid(), seeded_user_id, mt.id, mt.code, mt.type, mt.title, mt.description,
            mt.requirement_type,
            CASE WHEN mt.type = 'DAILY' THEN day_start ELSE week_start END,
            CASE WHEN mt.type = 'DAILY' THEN day_end ELSE week_end END,
            GREATEST(mt.target_value - 1, 1), mt.target_value, mt.reward_xp, 'ACTIVE',
            NULL, NOW(), NOW()
        FROM mission_templates mt
        WHERE mt.code IN ('PLAY_10_MINUTES', 'PLAY_60_MINUTES')
        ON CONFLICT (user_id, mission_template_id, period_start) DO UPDATE SET
            progress_value = GREATEST(user_missions.progress_value, EXCLUDED.progress_value),
            status = CASE WHEN user_missions.status = 'COMPLETED' THEN user_missions.status ELSE 'ACTIVE' END,
            updated_at = NOW();

        INSERT INTO leaderboard_entries (
            id, leaderboard_id, period_id, user_id, score, rank, previous_rank, rank_change,
            games_played, valid_sessions, xp_earned, score_reached_at, is_disqualified,
            created_at, updated_at)
        SELECT gen_random_uuid(), ld.id, p.id, seeded_user_id, seed_user.score, seed_user.idx, seed_user.previous_rank, seed_user.previous_rank - seed_user.idx,
               seed_user.games_played, GREATEST(seed_user.games_played - 3, 1), seed_user.score,
               NOW() - ((12 - seed_user.idx) || ' hours')::interval, FALSE, NOW(), NOW()
        FROM leaderboard_definitions ld
        INNER JOIN leaderboard_periods p ON p.leaderboard_id = ld.id
        WHERE (ld.code = 'GLOBAL_WEEKLY_XP' AND p.code = weekly_code)
           OR (ld.code = 'GLOBAL_MONTHLY_XP' AND p.code = monthly_code)
           OR (ld.code = 'GLOBAL_ALL_TIME_XP' AND p.code = all_time_code)
        ON CONFLICT (leaderboard_id, period_id, user_id) DO UPDATE SET
            score = GREATEST(leaderboard_entries.score, EXCLUDED.score),
            previous_rank = COALESCE(leaderboard_entries.previous_rank, EXCLUDED.previous_rank),
            games_played = GREATEST(leaderboard_entries.games_played, EXCLUDED.games_played),
            valid_sessions = GREATEST(leaderboard_entries.valid_sessions, EXCLUDED.valid_sessions),
            xp_earned = GREATEST(leaderboard_entries.xp_earned, EXCLUDED.xp_earned),
            score_reached_at = LEAST(leaderboard_entries.score_reached_at, EXCLUDED.score_reached_at),
            is_disqualified = FALSE,
            updated_at = NOW();

        INSERT INTO community_posts (
            id, author_id, game_id, type, title, content, slug, status,
            comment_count, reaction_count, view_count, created_at, updated_at)
        VALUES (
            post_id, seeded_user_id, NULL, seed_user.post_type, seed_user.post_title,
            seed_user.post_content, 'phase22-demo-post-' || LPAD(seed_user.idx::text, 2, '0'),
            'published', 0, 0, seed_user.idx * 17,
            NOW() - (seed_user.idx || ' hours')::interval, NOW())
        ON CONFLICT (slug) DO UPDATE SET
            title = EXCLUDED.title,
            content = EXCLUDED.content,
            status = 'published',
            view_count = GREATEST(community_posts.view_count, EXCLUDED.view_count),
            deleted_at = NULL,
            updated_at = NOW();

        INSERT INTO community_comments (
            id, post_id, author_id, parent_id, content, status, reaction_count, created_at, updated_at)
        VALUES (
            comment_id, target_post_id, seeded_user_id, NULL,
            'Setuju, ini juga cocok untuk pemain yang ingin progres santai.',
            'published', 0, NOW() - ((seed_user.idx + 1) || ' hours')::interval, NOW())
        ON CONFLICT (id) DO UPDATE SET
            content = EXCLUDED.content,
            status = 'published',
            deleted_at = NULL,
            updated_at = NOW();

        INSERT INTO community_reactions (
            id, user_id, target_type, target_id, reaction, created_at)
        VALUES (
            reaction_id, seeded_user_id, 'post',
            ('c2200000-0000-4000-8000-' || LPAD((CASE WHEN seed_user.idx = 10 THEN 1 ELSE seed_user.idx + 1 END)::text, 12, '0'))::uuid,
            CASE WHEN seed_user.idx % 3 = 0 THEN 'helpful' WHEN seed_user.idx % 2 = 0 THEN 'love' ELSE 'like' END,
            NOW() - (seed_user.idx || ' hours')::interval)
        ON CONFLICT (user_id, target_type, target_id) DO UPDATE SET
            reaction = EXCLUDED.reaction;

        IF seed_user.idx > 1 THEN
            INSERT INTO user_follows (follower_id, following_id, created_at)
            VALUES (
                seeded_user_id,
                ('22222222-2222-4222-8222-' || LPAD((seed_user.idx - 1)::text, 12, '0'))::uuid,
                NOW() - (seed_user.idx || ' days')::interval)
            ON CONFLICT DO NOTHING;
        END IF;

        IF demo_user_id IS NOT NULL THEN
            INSERT INTO user_follows (follower_id, following_id, created_at)
            VALUES (seeded_user_id, demo_user_id, NOW() - (seed_user.idx || ' days')::interval)
            ON CONFLICT DO NOTHING;
        END IF;

        INSERT INTO community_activity (
            id, user_id, activity_type, game_id, entity_type, entity_id, metadata, created_at)
        VALUES
            (('c2230000-0000-4000-8000-' || LPAD(seed_user.idx::text, 12, '0'))::uuid, seeded_user_id, 'POST_CREATED', NULL, 'post', post_id, jsonb_build_object('seed', 'phase22'), NOW() - (seed_user.idx || ' hours')::interval),
            (('c2240000-0000-4000-8000-' || LPAD(seed_user.idx::text, 12, '0'))::uuid, seeded_user_id, 'COMMENT_CREATED', NULL, 'comment', comment_id, jsonb_build_object('seed', 'phase22'), NOW() - ((seed_user.idx + 1) || ' hours')::interval),
            (('c2250000-0000-4000-8000-' || LPAD(seed_user.idx::text, 12, '0'))::uuid, seeded_user_id, 'REACTION_CREATED', NULL, 'post', reaction_id, jsonb_build_object('seed', 'phase22'), NOW() - ((seed_user.idx + 2) || ' hours')::interval)
        ON CONFLICT (id) DO UPDATE SET
            metadata = EXCLUDED.metadata,
            created_at = EXCLUDED.created_at;
    END LOOP;

    UPDATE community_posts p
    SET comment_count = c.count,
        updated_at = NOW()
    FROM (
        SELECT post_id, COUNT(*)::integer AS count
        FROM community_comments
        WHERE deleted_at IS NULL AND status = 'published'
        GROUP BY post_id
    ) c
    WHERE p.id = c.post_id
      AND p.slug LIKE 'phase22-demo-post-%';

    UPDATE community_posts p
    SET reaction_count = r.count,
        updated_at = NOW()
    FROM (
        SELECT target_id, COUNT(*)::integer AS count
        FROM community_reactions
        WHERE target_type = 'post'
        GROUP BY target_id
    ) r
    WHERE p.id = r.target_id
      AND p.slug LIKE 'phase22-demo-post-%';

    WITH ranked AS (
        SELECT
            e.id,
            ROW_NUMBER() OVER (PARTITION BY e.leaderboard_id, e.period_id ORDER BY e.score DESC, e.score_reached_at ASC, e.user_id ASC)::integer AS new_rank
        FROM leaderboard_entries e
        INNER JOIN leaderboard_periods p ON p.id = e.period_id
        WHERE p.code IN (weekly_code, monthly_code, all_time_code)
          AND e.is_disqualified = FALSE
    )
    UPDATE leaderboard_entries e
    SET rank = ranked.new_rank,
        rank_change = COALESCE(e.previous_rank, ranked.new_rank) - ranked.new_rank,
        updated_at = NOW()
    FROM ranked
    WHERE e.id = ranked.id;
END $$;

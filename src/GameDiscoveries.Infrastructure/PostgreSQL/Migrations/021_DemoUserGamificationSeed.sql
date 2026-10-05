-- Demo gamification seed for demo@gamediscoveries.com
-- Idempotent and safe: no-op if the demo user is missing.

DO $$
DECLARE
    demo_user_id UUID;
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
    weekly_period_id UUID;
    monthly_period_id UUID;
    all_time_period_id UUID;
BEGIN
    SELECT id INTO demo_user_id
    FROM users
    WHERE LOWER(email) = 'demo@gamediscoveries.com'
    LIMIT 1;

    IF demo_user_id IS NULL THEN
        RETURN;
    END IF;

    jakarta_now := timezone('Asia/Jakarta', NOW());
    day_start := date_trunc('day', jakarta_now) AT TIME ZONE 'Asia/Jakarta';
    day_end := (date_trunc('day', jakarta_now) + INTERVAL '1 day') AT TIME ZONE 'Asia/Jakarta';
    week_start := date_trunc('week', jakarta_now) AT TIME ZONE 'Asia/Jakarta';
    week_end := (date_trunc('week', jakarta_now) + INTERVAL '7 days') AT TIME ZONE 'Asia/Jakarta';
    month_start := date_trunc('month', jakarta_now) AT TIME ZONE 'Asia/Jakarta';
    month_end := (date_trunc('month', jakarta_now) + INTERVAL '1 month') AT TIME ZONE 'Asia/Jakarta';
    weekly_code := 'WEEKLY_' || to_char(date_trunc('week', jakarta_now), 'IYYY-"W"IW');
    monthly_code := 'MONTHLY_' || to_char(jakarta_now, 'YYYY-MM');

    INSERT INTO user_progress (
        user_id, total_xp, level, current_level_xp, current_streak, longest_streak,
        games_played, favorites_count, unique_games_played, last_activity_at,
        streak_start_date, last_qualifying_activity_date, streak_status,
        streak_freeze_count, streak_updated_at, created_at, updated_at)
    VALUES (
        demo_user_id, 2450, 9, 250, 7, 14,
        31, 8, 18, NOW(),
        (jakarta_now::date - 6), jakarta_now::date, 'ACTIVE',
        1, NOW(), NOW(), NOW())
    ON CONFLICT (user_id) DO UPDATE SET
        total_xp = GREATEST(user_progress.total_xp, 2450),
        level = GREATEST(user_progress.level, 9),
        current_level_xp = GREATEST(user_progress.current_level_xp, 250),
        current_streak = GREATEST(user_progress.current_streak, 7),
        longest_streak = GREATEST(user_progress.longest_streak, 14),
        games_played = GREATEST(user_progress.games_played, 31),
        favorites_count = GREATEST(user_progress.favorites_count, 8),
        unique_games_played = GREATEST(user_progress.unique_games_played, 18),
        last_activity_at = NOW(),
        streak_start_date = COALESCE(user_progress.streak_start_date, (jakarta_now::date - 6)),
        last_qualifying_activity_date = jakarta_now::date,
        streak_status = 'ACTIVE',
        streak_freeze_count = GREATEST(user_progress.streak_freeze_count, 1),
        streak_updated_at = NOW(),
        updated_at = NOW();

    INSERT INTO xp_transactions (
        id, transaction_id, user_id, event_type, reference_type, reference_id,
        rule_code, xp_amount, description, metadata_json, created_at)
    VALUES
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000001', demo_user_id, 'GAME_SESSION_END', 'GAME_SESSION', 'demo-session-001', 'VALID_GAME_SESSION', 300, 'Demo valid game sessions', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '6 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000002', demo_user_id, 'GAME_SESSION_END', 'GAME_SESSION', 'demo-session-002', 'REPEATED_GAME_PLAY', 250, 'Demo repeated play bonus', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '5 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000003', demo_user_id, 'FAVORITE_ADDED', 'GAME', 'demo-favorite-001', 'FIRST_FAVORITE', 200, 'Demo favorite games', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '5 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000004', demo_user_id, 'RATING_CREATED', 'GAME', 'demo-rating-001', 'FIRST_RATING', 150, 'Demo game ratings', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '4 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000005', demo_user_id, 'REVIEW_CREATED', 'GAME', 'demo-review-001', 'FIRST_REVIEW', 250, 'Demo review contribution', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '4 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000006', demo_user_id, 'DAILY_MISSION_COMPLETED', 'MISSION', 'demo-daily-001', 'DAILY_MISSION_COMPLETED', 350, 'Demo daily mission XP', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '3 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000007', demo_user_id, 'WEEKLY_CHALLENGE_COMPLETED', 'MISSION', 'demo-weekly-001', 'WEEKLY_CHALLENGE_COMPLETED', 450, 'Demo weekly challenge XP', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '2 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000008', demo_user_id, 'ACHIEVEMENT_UNLOCK', 'ACHIEVEMENT', 'FIRST_DISCOVERY', 'ACHIEVEMENT_UNLOCK', 250, 'Demo achievement XP', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '2 days'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000009', demo_user_id, 'STREAK_MILESTONE', 'STREAK_MILESTONE', '7', 'STREAK_MILESTONE', 200, 'Demo 7 day streak', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '1 day'),
        (gen_random_uuid(), 'd2100000-0000-4000-8000-000000000010', demo_user_id, 'GAME_SHARED', 'GAME', 'demo-share-001', 'GAME_SHARED', 50, 'Demo share activity', '{"seed":"demo","source":"phase21"}', NOW() - INTERVAL '12 hours')
    ON CONFLICT (transaction_id) DO NOTHING;

    FOR i IN 0..6 LOOP
        INSERT INTO user_activity_days (
            id, user_id, activity_date, first_activity_at, last_activity_at,
            qualifying_session_count, created_at, updated_at)
        VALUES (
            gen_random_uuid(), demo_user_id, (jakarta_now::date - i),
            ((jakarta_now::date - i)::timestamp + TIME '09:00') AT TIME ZONE 'Asia/Jakarta',
            ((jakarta_now::date - i)::timestamp + TIME '09:35') AT TIME ZONE 'Asia/Jakarta',
            2, NOW(), NOW())
        ON CONFLICT (user_id, activity_date) DO UPDATE SET
            qualifying_session_count = GREATEST(user_activity_days.qualifying_session_count, 2),
            last_activity_at = EXCLUDED.last_activity_at,
            updated_at = NOW();
    END LOOP;

    INSERT INTO streak_history (
        id, user_id, event_type, streak_value, activity_date, previous_streak,
        new_streak, reason, metadata_json, created_at)
    VALUES (
        gen_random_uuid(), demo_user_id, 'STREAK_MILESTONE', 7, jakarta_now::date,
        6, 7, 'Demo seed 7 day streak', '{"seed":"demo","source":"phase21"}', NOW())
    ON CONFLICT DO NOTHING;

    INSERT INTO user_streak_milestones (id, user_id, days, achieved_at, reward_xp)
    VALUES
        (gen_random_uuid(), demo_user_id, 3, NOW() - INTERVAL '4 days', 0),
        (gen_random_uuid(), demo_user_id, 7, NOW(), 50)
    ON CONFLICT (user_id, days) DO NOTHING;

    INSERT INTO user_missions (
        id, user_id, mission_template_id, code, type, title, description,
        requirement_type, period_start, period_end, progress_value, target_value,
        reward_xp, status, completed_at, created_at, updated_at)
    SELECT
        gen_random_uuid(), demo_user_id, mt.id, mt.code, mt.type, mt.title, mt.description,
        mt.requirement_type,
        CASE WHEN mt.type = 'DAILY' THEN day_start ELSE week_start END,
        CASE WHEN mt.type = 'DAILY' THEN day_end ELSE week_end END,
        mt.target_value, mt.target_value, mt.reward_xp, 'COMPLETED',
        NOW() - INTERVAL '2 hours', NOW(), NOW()
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
        gen_random_uuid(), demo_user_id, mt.id, mt.code, mt.type, mt.title, mt.description,
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

    INSERT INTO user_achievement_unlocks (
        id, user_id, achievement_definition_id, unlocked_at, progress_value,
        target_value, reward_transaction_id, is_notified, created_at, updated_at)
    SELECT
        gen_random_uuid(), demo_user_id, ad.id, NOW() - (ad.sort_order || ' minutes')::interval,
        ad.target_value, ad.target_value, NULL, FALSE, NOW(), NOW()
    FROM achievement_definitions ad
    WHERE ad.code IN ('FIRST_DISCOVERY', 'FIRST_SESSION', 'FIRST_FAVORITE', 'FIRST_RATING', 'FIRST_REVIEW', 'THREE_DAY_STREAK', 'WEEK_WARRIOR', 'LEVEL_5')
    ON CONFLICT (user_id, achievement_definition_id) DO UPDATE SET
        progress_value = GREATEST(user_achievement_unlocks.progress_value, EXCLUDED.progress_value),
        revoked_at = NULL,
        updated_at = NOW();

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

    SELECT id INTO weekly_period_id FROM leaderboard_periods WHERE code = weekly_code;
    SELECT id INTO monthly_period_id FROM leaderboard_periods WHERE code = monthly_code;
    SELECT id INTO all_time_period_id FROM leaderboard_periods WHERE code = all_time_code;

    INSERT INTO leaderboard_entries (
        id, leaderboard_id, period_id, user_id, score, rank, previous_rank, rank_change,
        games_played, valid_sessions, xp_earned, score_reached_at, is_disqualified,
        created_at, updated_at)
    SELECT gen_random_uuid(), ld.id, p.id, demo_user_id, 2450, 1, 3, 2,
           31, 24, 2450, NOW() - INTERVAL '12 hours', FALSE, NOW(), NOW()
    FROM leaderboard_definitions ld
    INNER JOIN leaderboard_periods p ON p.leaderboard_id = ld.id
    WHERE (ld.code = 'GLOBAL_WEEKLY_XP' AND p.id = weekly_period_id)
       OR (ld.code = 'GLOBAL_MONTHLY_XP' AND p.id = monthly_period_id)
       OR (ld.code = 'GLOBAL_ALL_TIME_XP' AND p.id = all_time_period_id)
    ON CONFLICT (leaderboard_id, period_id, user_id) DO UPDATE SET
        score = GREATEST(leaderboard_entries.score, 2450),
        rank = 1,
        previous_rank = COALESCE(leaderboard_entries.previous_rank, 3),
        rank_change = GREATEST(leaderboard_entries.rank_change, 2),
        games_played = GREATEST(leaderboard_entries.games_played, 31),
        valid_sessions = GREATEST(leaderboard_entries.valid_sessions, 24),
        xp_earned = GREATEST(leaderboard_entries.xp_earned, 2450),
        score_reached_at = LEAST(leaderboard_entries.score_reached_at, NOW() - INTERVAL '12 hours'),
        is_disqualified = FALSE,
        updated_at = NOW();
END $$;

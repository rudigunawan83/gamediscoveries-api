# Phase 06 — Streak System

## Architecture

```
VALID GAME SESSION → user_activity_days (1/day) → Streak Engine → user_progress
                                                      ↓
                                               streak_history / milestones → XP Engine
```

Source of truth for counters: `user_progress.current_streak` / `longest_streak`.

Canonical daily qualification: `user_activity_days` UNIQUE `(user_id, activity_date)`.

Qualifying activity: **valid game session only** (`is_valid`, `active_seconds >= 30`).  
`GAME_VIEW` / `GAME_START` / favorites do **not** count.

Timezone: `Streak:TimeZone` (default `Asia/Jakarta`).

## States

| Status | Meaning |
|--------|---------|
| ACTIVE | Qualified today |
| AT_RISK | Streak > 0, yesterday qualified, not yet today |
| BROKEN | Missed day(s) without protection |
| FROZEN | Freeze just consumed (display may resolve to ACTIVE after continue) |

## Freeze

- Max stored: 2 (configurable)
- Default new users: 0
- Consumes on exactly **one** missed day between last activity and today

## Milestones

3 / 7 / 14 / 30 / 60 / 100 / 365 days.  
XP via `STREAK_MILESTONE` rule, unique per `userId:days`.

## APIs

User:

- `GET /api/v1/me/streak`
- `GET /api/v1/me/streak/history`
- `GET /api/v1/me/progress` includes `streak` snapshot

Admin:

- `GET /api/v1/admin/gamification/streaks`
- `GET /api/v1/admin/users/{id}/streak`
- `POST .../streak/freeze`
- `POST .../streak/freeze/remove`
- `POST .../streak/reset`

## Mission integration

`PLAY_5_DAYS` / `ACTIVE_DAYS` prefers counting `user_activity_days` when the table exists.

## Flutter

Consume server streak endpoints only — do not calculate streak client-side.

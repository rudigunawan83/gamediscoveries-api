# Phase 05 — Daily Mission & Weekly Challenge

## Architecture

```
User Activity → Analytics / Domain hooks → Mission Engine → XP Engine → user_progress
```

- Templates: `mission_templates`
- Instances: `user_missions` (snapshot title/target/reward/requirement)
- Timezone: `Missions:TimeZone` (default `Asia/Jakarta`)
- Assignment: lazy on `GET /api/v1/me/missions` and on activity
- Progress: recomputed from validated `game_play_sessions` / `user_favorites`
- Rewards: `IXpEngine.AwardAsync` with `USER_MISSION` + mission instance id (idempotent)

Community `/community/challenges` is a separate social feature — do not reuse.

## Periods

- Daily: local 00:00 → 23:59:59.999
- Weekly: Monday 00:00 → Sunday 23:59:59.999 (product timezone)

## User APIs

| Method | Path |
|--------|------|
| GET | `/api/v1/me/missions` |
| GET | `/api/v1/me/missions/history` |
| GET | `/api/v1/me/missions/{id}` |

## Admin APIs

| Method | Path | Role |
|--------|------|------|
| GET | `/api/v1/admin/gamification/missions/templates` | Moderator+ |
| POST/PUT | templates create/update | SuperAdmin |
| POST | activate/deactivate | SuperAdmin |
| GET | `/api/v1/admin/gamification/missions/analytics` | Moderator+ |

## Flutter

Consume server responses only. Do not calculate progress/completion/XP on device.

## Frontend

- `/missions` — daily + weekly UI
- `/admin/gamification/missions` — templates + analytics
- `/progress` links to missions

## Config

```json
"Missions": {
  "Enabled": true,
  "TimeZone": "Asia/Jakarta",
  "DailyMissionCount": 3,
  "WeeklyChallengeCount": 2,
  "NewUserXpThreshold": 100
}
```

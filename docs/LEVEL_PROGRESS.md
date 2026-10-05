# Phase 04 — Level + User Progress + Admin Gamification

## Overview

XP transactions remain the immutable ledger (Phase 03). `user_progress.total_xp` is the current balance. Level is **derived** from total XP via `ILevelService` / `LevelCalculator` using `gamification_levels`.

```
XP Transaction → TotalXp → Level Calculator → Level / Progress %
```

## Level curve

Stored in `gamification_levels` (seeded 1–100). Requirements increase by `50 * n` XP to reach level `n`:

| Level | RequiredTotalXp | Title |
|------:|----------------:|-------|
| 1 | 0 | Newcomer |
| 2 | 100 | Newcomer |
| 3 | 250 | Newcomer |
| 5 | 700 | Explorer |
| 10 | 2700 | Game Hunter |
| 20 | … | Adventurer |
| 30 | … | Game Master |
| 50 | … | Game Legend |
| 100 | … | Ultimate Discoverer |

Titles and thresholds are editable by `SuperAdmin`. Historical XP transactions are never rewritten when levels change.

## User APIs

### `GET /api/v1/me/progress`

Returns user profile, computed level info (including next title), and live stats:

- `totalGameSessions` — valid sessions
- `uniqueGamesPlayed` — distinct games with valid sessions
- `favorites` — from `user_favorites`
- `currentStreak` / `longestStreak` — placeholders (Phase 06)

### `GET /api/v1/me/xp/transactions`

Paged XP history. Supports `page`, `pageSize`, `ruleCode`, `dateFrom`, `dateTo` (also accepts legacy `limit`/`offset`/`from`/`to`).

## Flutter contract

Call the same endpoints. **Do not** calculate level on the client.

```json
{
  "level": 5,
  "title": "Explorer",
  "totalXp": 850,
  "currentLevelXp": 150,
  "nextLevelXp": 300,
  "progressPercentage": 50,
  "nextLevel": 6,
  "nextTitle": "Explorer"
}
```

(Nested under `data.level` inside the standard `ApiResponse` envelope.)

## Admin APIs

| Endpoint | Role |
|----------|------|
| `GET /api/v1/admin/gamification/overview` | Moderator+ |
| `GET /api/v1/admin/users` | Moderator+ |
| `GET /api/v1/admin/users/{id}` | Moderator+ |
| `POST /api/v1/admin/users/{id}/xp-adjustments` | SuperAdmin |
| `POST /api/v1/admin/users/{id}/gamification/reset` | SuperAdmin |
| `POST /api/v1/admin/users/{id}/streak/reset` | SuperAdmin |
| `POST /api/v1/admin/users/{id}/suspend\|unsuspend` | SuperAdmin |
| `GET/POST/PUT …/gamification/levels` | View: Moderator+ / Mutate: SuperAdmin |
| `GET /api/v1/admin/audit-logs` | Admin+ |

XP adjustments always create an `ADMIN_ADJUSTMENT` transaction and an append-only `audit_logs` row. Total XP cannot go below 0.

Gamification reset creates a reversing adjustment (preserves history), zeros streak counters, and writes `ADMIN_GAMIFICATION_RESET`.

## Events

- `LEVEL_UP` — emitted server-side when an award increases level (`oldLevel`, `newLevel`, `totalXp`)
- Suspended users cannot log in (`status != active`)

## Frontend

- Public: `/progress`, `/progress/xp`
- Admin: `/admin/gamification`, `/users`, `/levels`, `/audit-logs`

## Not in this phase

Missions, streak engine, achievements/badges, leaderboards, rewards, anti-fraud.

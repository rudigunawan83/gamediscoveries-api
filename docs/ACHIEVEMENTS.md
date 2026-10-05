# Phase 07 — Achievement & Badge System

Server-authoritative achievements that evaluate validated user behavior, unlock once, and reward XP through the existing XP Engine.

## Architecture

```
User action → Domain service (session / favorite / review / level / mission / streak)
           → IAchievementActivitySink
           → AchievementService.EvaluateForUserAsync(trigger)
           → Requirement metrics (canonical SQL)
           → user_achievement_unlocks (UNIQUE user+definition)
           → IXpEngine AwardAsync(ACHIEVEMENT_UNLOCK)
           → Analytics ACHIEVEMENT_UNLOCKED
```

Frontend/mobile never unlocks achievements. Clients only read state.

## Tables

| Table | Purpose |
|---|---|
| `achievement_definitions` | Catalog (code unique, season_id nullable) |
| `user_achievement_unlocks` | Permanent unlocks (revoked_at soft revoke). Named to avoid conflict with Community `user_achievements`. |
| `achievement_history` | Append-only UNLOCKED / GRANTED / REVOKED / admin events |

Progress is computed from existing sources (sessions, favorites, reviews, user_progress, user_missions). No duplicate progress counter tables.

## XP

- Rule: `ACHIEVEMENT_UNLOCK`
- ReferenceType: `ACHIEVEMENT`
- ReferenceId: `{userId}:{definitionId}` (idempotent)

## User APIs

- `GET /api/v1/me/achievements`
- `GET /api/v1/me/achievements/unlocked`
- `GET /api/v1/me/achievements/in-progress`
- `GET /api/v1/me/achievements/recent`
- `GET /api/v1/me/achievements/{code}`

## Admin APIs

- `GET/POST /api/v1/admin/gamification/achievements`
- `GET/PUT .../{id}`, activate/deactivate, `.../{id}/users`
- `GET /api/v1/admin/users/{userId}/achievements`
- Grant/revoke: SuperAdmin only, audited

## Secret achievements

Before unlock: title `???`, generic description, no target/reward.
After unlock: full details.

## Notes

- Community badges (`achievements` / `user_achievements`) remain for community feed; gamification catalog is separate.
- Notification delivery deferred to Phase 14 (`ACHIEVEMENT_UNLOCKED` event is ready).
- Lazy evaluation on `GET /me/achievements` backfills historical eligibility.

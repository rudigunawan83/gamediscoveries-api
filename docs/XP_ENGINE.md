# XP Engine (Phase 03)

Server-authoritative XP. Clients never set XP amounts.

## Rules (config: `Xp` section)

| Rule | Amount | Unique key |
|------|--------|------------|
| FIRST_GAME_DISCOVERY | 20 | user |
| NEW_GAME_DISCOVERED | 15 | user+game |
| NEW_GENRE_DISCOVERED | 20 | user+category |
| VALID_GAME_SESSION | 10 | user+session |
| SESSION_MILESTONE_2M/5M/10M | 10/20/30 | highest only per session |
| FAVORITE_ADDED | 10 | user+game |
| RATING_CREATED | 10 | user+game |
| REVIEW_CREATED | 30 | user+game |

Daily cap: 500 XP (configurable).

## APIs

- `GET /api/v1/me/xp`
- `GET /api/v1/me/xp/transactions`
- `GET /api/v1/admin/users/{userId}/xp`
- `POST /api/v1/admin/users/{userId}/xp/adjust` (SuperAdmin; audited)
- `POST /api/v1/admin/users/{userId}/xp-adjustments` (SuperAdmin; Phase 04)
- `POST /api/v1/admin/xp/transactions/{transactionId}/reverse` (SuperAdmin)

Level derivation, progress UI, and admin gamification: see [`LEVEL_PROGRESS.md`](LEVEL_PROGRESS.md).

## Integration

- `GAME_SESSION_END` → `GameSessionXpEventHandler` → `IXpEngine`
- Level recalculated on every successful award; `LEVEL_UP` analytics emitted on increase
- Favorites / ratings / reviews hooks call `IXpEngine`
- Favorite create → `AddFavoriteHandler`
- Review create → `CommunityService.UpsertReviewAsync` (create only)

Anonymous users: sessions tracked, no persistent XP.

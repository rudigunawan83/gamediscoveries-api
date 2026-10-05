# Phase 10 — Leaderboard & Competition

Server-authoritative XP leaderboards. Scores project from eligible `xp_transactions` only.

## Score pipeline

```
XP Engine → xp_transactions → ILeaderboardXpSink → leaderboard_entries → ranks
```

Excluded by default: `ADMIN_ADJUSTMENT`, `COMPETITION_REWARD`.
Reversals (`XP_REVERSAL`) adjust score. Competition rewards never re-enter the same competition score.

## Boards

| Code | Type |
|------|------|
| GLOBAL_WEEKLY_XP | Weekly (Mon 00:00 Asia/Jakarta) |
| GLOBAL_MONTHLY_XP | Monthly |
| GLOBAL_ALL_TIME_XP | Lifetime eligible XP |

Version: `LEADERBOARD_V1`

## APIs

- `GET /api/v1/leaderboards`
- `GET /api/v1/leaderboards/{code}`
- `GET /api/v1/leaderboards/{code}/me`
- `GET /api/v1/leaderboards/{code}/history`
- `GET /api/v1/me/leaderboards/history`
- `GET /api/v1/competitions`
- `POST /api/v1/competitions/{code}/join`
- Admin rebuild / disqualify / settle

## Future phases

- Phase 11: `FriendsScopeProvider`
- Phase 12: season + richer reward settlement
- Phase 13: fraud flags → disqualify
- Phase 14: rank milestone notifications
- Phase 15: experiment via `LeaderboardVersion`

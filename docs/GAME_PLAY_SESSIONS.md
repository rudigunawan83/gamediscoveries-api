# Game Play Sessions (Phase 02)

Trusted session lifecycle for web + future Flutter.

## Endpoints

- `POST /api/v1/games/{gameId}/sessions/start`
- `POST /api/v1/games/sessions/{sessionId}/heartbeat`
- `POST /api/v1/games/sessions/{sessionId}/pause`
- `POST /api/v1/games/sessions/{sessionId}/resume`
- `POST /api/v1/games/sessions/{sessionId}/end`
- `GET /api/v1/games/sessions/{sessionId}`
- Admin: `GET /api/v1/admin/analytics/sessions/overview`

## Semantics

| Concept | Meaning |
|--------|---------|
| GAME_VIEW | Page/detail viewed (analytics only) |
| GAME_START | Player launched |
| GAME_SESSION_START | Tracking session created |
| ActiveSeconds | Server-calculated from heartbeat intervals |
| IsValid | Eligible later for XP (Phase 03); not awarded here |

## Flutter / WebView

Flutter should own:

- app lifecycle
- session start/pause/resume/end
- heartbeat scheduling
- auth + anonymousId

WebView HTML5 games are **not** trusted for duration.

See also: `docs/ANALYTICS_EVENTS.md`

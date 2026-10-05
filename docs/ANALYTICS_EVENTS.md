# Analytics Events API (Phase 01)

Flutter / mobile clients should use the same backend endpoints as the web app.

## Endpoints

- `POST /api/v1/events`
- `POST /api/v1/events/batch`
- Legacy: `POST /api/v1/analytics/events`
- Admin overview: `GET /api/v1/admin/analytics/overview` (Moderator/Admin JWT)

## Auth behavior

- If `Authorization: Bearer <jwt>` is present, `UserId` is taken from the token.
- Clients must **not** send a trusted `userId`. Spoofed values are rejected.
- Anonymous clients must send a persistent `anonymousId` (UUID).

## Idempotency

- Every event should include a client-generated `eventId` (UUID).
- Replays with the same `eventId` are accepted as duplicates (idempotent success).

## Batch

```json
{
  "events": [
    {
      "eventId": "uuid-1",
      "eventType": "GAME_VIEW",
      "anonymousId": "uuid-anon",
      "sessionId": "uuid-session",
      "gameId": "uuid-game",
      "source": "MOBILE",
      "platform": "ANDROID",
      "metadata": { "position": 1 },
      "occurredAt": "2026-10-05T10:00:00Z"
    }
  ]
}
```

Limits (configurable):

- Max batch size: 100
- Max metadata size: 16 KB

## Sources / platforms

- Source: `WEB | MOBILE | GAME | API | ADMIN | SYSTEM`
- Platform: `WEB | ANDROID | IOS | DESKTOP | UNKNOWN`

## Notes for Flutter + WebView

- Flutter owns trusted app/session context (`anonymousId`, `sessionId`, auth token).
- HTML5 / GameMonetize games should not be patched.
- Game play events can be emitted by Flutter around the WebView lifecycle:
  - `GAME_START`
  - `GAME_SESSION_START`
  - `GAME_SESSION_END`
- Client-provided duration/active time in metadata is informational only.
- Trusted play duration/active time: use Phase 02 Game Play Session APIs
  (`docs/GAME_PLAY_SESSIONS.md`).

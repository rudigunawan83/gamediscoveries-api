# Game Providers

GameDiscoveries treats external catalogs (starting with **GameMonetize**) as providers.
The frontend never talks to GameMonetize. It only consumes GameDiscoveries APIs.

## Architecture

```text
GameMonetize RSS/JSON feeds
        ↓
GameMonetizeClient (+ retry/backoff)
        ↓
GameMonetizeMapper / TagNormalizer / ProviderCategoryMapper
        ↓
GameFeedImportService (idempotent upsert)
        ↓
PostgreSQL (games + game_provider_mappings)
        ↓
Meilisearch index (best-effort) + Redis cache invalidation
        ↓
Public Game / Discovery API
        ↓
Next.js frontend
```

## Abstraction

- `IGameProvider` — provider-agnostic contract (`GetGamesAsync`, `GetGameAsync`, `GetCategoriesAsync`, `GetGamePlayUrlAsync`)
- `GameMonetizeProvider` — GameMonetize implementation
- `GameProviderType` — enum for future providers (only `GameMonetize` implemented)

To add another provider later:

1. Implement `IGameProvider` in Infrastructure.
2. Register it in DI.
3. Add feed/import path (or a shared sync orchestrator).
4. Keep `games` / public API provider-agnostic.

## Configuration

| Setting | Purpose |
|---|---|
| `GameMonetize__Enabled` | Enable/disable provider HTTP + sync |
| `GameMonetize__Feeds__*` | Per-feed JSON URLs |
| `GameMonetize__TimeoutSeconds` | HTTP timeout |
| `GameMonetize__MaxRetries` | Transient retry count |
| `GameFeedSync__Enabled` | Background sync host |
| `GameFeedSync__AdminApiKey` | Protects admin sync endpoints |
| `GameFeedSync__*IntervalMinutes` | Per-feed schedule |

Never commit API keys or production secrets.

## Manual sync

Header:

```http
X-GameDiscoveries-Admin-Key: <GameFeedSync__AdminApiKey>
```

Endpoints:

```http
POST /api/v1/admin/game-feeds/sync
POST /api/v1/admin/game-feeds/sync-all
POST /api/v1/admin/providers/gamemonetize/sync
GET  /api/v1/admin/providers
GET  /api/v1/admin/providers/gamemonetize/status
```

Example:

```bash
curl -X POST "https://api.gamediscoveries.com/api/v1/admin/providers/gamemonetize/sync" \
  -H "Content-Type: application/json" \
  -H "X-GameDiscoveries-Admin-Key: $ADMIN_KEY" \
  -d '{"feedType":"Latest"}'
```

## Identity & availability

- External identity: `(provider_id, provider_game_id)` unique.
- `raw_payload` JSONB stores original provider item for audit/reprocess.
- `last_synced_at` / `last_seen_at` track sync hits.
- `availability_status`: `active` | `unavailable` | …
- After a successful **Latest** sync, mappings not seen are marked `unavailable` and related games are archived (not deleted). Reappearance reactivates them.

## Troubleshooting

| Symptom | Check |
|---|---|
| Sync returns 401 | `GameFeedSync__AdminApiKey` header mismatch / empty |
| Sync creates 0 games | `GameMonetize__Enabled`, feed URL, mapper skip reasons in logs |
| Catalog missing titles | Game `status` archived / mapping `unavailable` |
| Search stale | Meilisearch errors in logs (DB sync still authoritative) |
| Home discoveries stale | Redis key `discoveries:home:v1` invalidation logs |

## Frontend rule

Frontend must use `playUrl` / catalog fields from GameDiscoveries only.
No GameMonetize SDK, credentials, or feed URLs in Next.js.

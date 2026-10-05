# Phase 08 — Discovery Score & Trending

Deterministic, server-authoritative ranking for how valuable a game is to surface right now.

## Architecture

```
analytics_events / game_play_sessions / favorites / reviews
        ↓
Metric aggregation (hourly/daily/weekly)
        ↓
Discovery Score Engine (components + weights)
        ↓
Trending snapshots (rank + rank change)
        ↓
Cached public APIs + home feed provider
```

## Score formula (v1)

```
DiscoveryScore =
  Popularity * 0.20
+ Engagement * 0.25
+ Quality    * 0.15
+ Momentum   * 0.20
+ Growth     * 0.10
+ Freshness  * 0.10
```

TrendingScore emphasizes recent activity + momentum + growth.

Weights live in `discovery_score_config` and `DiscoveryScore` options. Changing weights increments `ScoreVersion`.

## APIs

Public:
- `GET /api/v1/discovery?type=TRENDING&period=24h`
- `/discovery/trending|rising|popular|new-trending|most-*`
- `GET /api/v1/trending`
- `GET /api/v1/games/{id}/discovery-score`

Admin:
- overview / rankings / config / recalculate

## Background job

`DiscoveryScoreHostedService` every `AggregationIntervalMinutes` (default 60):
aggregate → recalculate scores → snapshots → cache bust

## Cache

`discovery:ranking:{type}:{period}:...` TTL 10 minutes. PostgreSQL remains source of truth.

## Integration with Phase 09

Personalized recommendations should consume:
- Discovery Score
- Trending Score
- component scores
as global prior signals — not replace them with client-side ranking.

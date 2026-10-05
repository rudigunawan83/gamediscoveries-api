# Phase 09 — Personalized Recommendation Engine

Rule-based personalized recommendations built on Phase 01–08 signals.

## Pipeline

```
User activity → Preference profile → Candidate generation
→ Scoring (personal + Discovery + Trending) → Diversity + MMR
→ Explanation → API (+ request/impression tracking)
```

## Algorithm

`PERSONALIZED_V1`

Score blend (normalized 0–100 in API):
- Personal relevance / content / preference
- Discovery Score & Trending Score (Phase 08)
- Freshness, novelty, engagement, exploration
- Repeat-play penalty (softer for favorites)
- Hard exclude disliked / low-rated games

## Profile levels

| Level | Interactions | Strategy bias |
|------|--------------|---------------|
| 0 | 0 | Cold start / trending |
| 1 | 1–2 | Light personalization |
| 2 | 3–9 | Genre + category |
| 3 | 10–49 | Strong personalization |
| 4 | 50+ | Full + diversity + exploration |

## APIs

- `GET /api/v1/recommendations?type=for-you`
- `GET /api/v1/recommendations/home`
- typed shelves: for-you, because-you-played, trending, new, hidden-gems
- `POST /api/v1/recommendations/{gameId}/feedback`
- `POST /api/v1/recommendations/impressions`
- Admin: `/api/v1/admin/recommendations/overview|debug`

## Tables

- `user_recommendation_profiles`
- `recommendation_requests`
- `recommendation_impressions`
- `recommendation_feedback`
- `recommendation_config`

## Phase 10 / 15

- Leaderboard can reuse engagement quality from recommended starts
- Experimentation uses `AlgorithmVersion` + `RecommendationRequestId` + impressions/clicks

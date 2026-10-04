# 🎮 GameDiscoveries API

Backend API for **GameDiscoveries.com**, a global game discovery platform designed to help users discover, play, and rediscover games through personalized recommendations, search, trending content, curated collections, and AI-powered game intelligence.

---

## 📌 Overview

GameDiscoveries API is the backend platform that powers:

- 🎮 Game catalog
- 🔎 Game search
- ✨ AI-powered discovery
- 🤖 Game metadata enrichment
- 🧠 Personalized recommendations
- 🔥 Trending games
- 🆕 New game discovery
- 📱 Mobile game discovery
- 👥 Multiplayer discovery
- ❤️ Favorites
- 🕘 Recently played games
- 📊 Product analytics
- 👨‍💻 Developer platform
- 📣 Advertising platform
- 🛠️ Administration
- 🔐 Authentication and authorization
- 🔌 External game provider integrations

The API is designed as a **Modular Monolith** using **Vertical Slice Architecture**.

The initial goal is to keep the system simple enough to develop and operate quickly while maintaining clear module boundaries for future horizontal scaling and selective service extraction.

---

# 🎯 Product Philosophy

GameDiscoveries is not intended to be just another game catalog.

The core product loop is:

```text
Discover
   ↓
Play
   ↓
Recommend
   ↓
Play Again
   ↓
Return
```

The API therefore focuses heavily on:

- discovery
- recommendation
- behavioral analytics
- game metadata
- personalization
- search
- game engagement

The catalog is the foundation.

The **Discovery Engine** is the core product.

---

# 🏗️ Architecture

## Architecture Style

```text
Modular Monolith
        +
Vertical Slice Architecture
        +
API-first
        +
Domain-oriented modules
```

High-level architecture:

```text
                         ┌──────────────────────┐
                         │    GameDiscoveries   │
                         │       Web App        │
                         │      Next.js 16      │
                         └──────────┬───────────┘
                                    │
                              HTTPS / JSON
                                    │
                                    ▼
                    ┌─────────────────────────────┐
                    │       GameDiscoveries API   │
                    │          .NET 10             │
                    │                             │
                    │      Modular Monolith       │
                    │                             │
                    │ ┌─────────────────────────┐ │
                    │ │ Catalog                 │ │
                    │ │ Providers               │ │
                    │ │ Discovery               │ │
                    │ │ Recommendation          │ │
                    │ │ Search                  │ │
                    │ │ Users                   │ │
                    │ │ Favorites               │ │
                    │ │ Analytics               │ │
                    │ │ Developer               │ │
                    │ │ Advertising             │ │
                    │ │ Campaign                │ │
                    │ │ Administration          │ │
                    │ └─────────────────────────┘ │
                    └───────┬────────┬────────────┘
                            │        │
                ┌───────────┘        └────────────┐
                ▼                                  ▼
       ┌────────────────┐                 ┌────────────────┐
       │  PostgreSQL 18 │                 │     Redis      │
       │ System of Record│                 │ Cache / State  │
       └────────────────┘                 └────────────────┘
                │
                ▼
       ┌────────────────┐
       │  Meilisearch   │
       │ Search Engine  │
       └────────────────┘
```

---

# 🧱 Technology Stack

| Area | Technology |
|---|---|
| Runtime | .NET 10 |
| Language | C# 14 |
| Framework | ASP.NET Core |
| Architecture | Modular Monolith |
| Application Pattern | Vertical Slice |
| Database | PostgreSQL 18 |
| Data Access | Dapper |
| Cache | Redis |
| Search | Meilisearch |
| Validation | FluentValidation |
| Authentication | JWT |
| External Authentication | OAuth/OIDC |
| API Documentation | OpenAPI |
| Logging | Serilog |
| Observability | OpenTelemetry |
| Metrics | Prometheus |
| Dashboard | Grafana |
| Logs | Loki |
| Containerization | Docker |
| CI/CD | GitLab CI/CD |
| AI Gateway | OpenAI-compatible API / 9router |
| Object Storage | S3-compatible Storage |
| CDN/WAF | Cloudflare |

---

# 📂 Project Structure

Recommended repository structure:

```text
apps/
└── api/
    │
    ├── GameDiscoveries.Api/
    │   ├── Program.cs
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   ├── Middleware/
    │   ├── Extensions/
    │   └── DependencyInjection/
    │
    ├── Modules/
    │   │
    │   ├── Catalog/
    │   │   ├── Application/
    │   │   ├── Domain/
    │   │   ├── Infrastructure/
    │   │   └── Endpoints/
    │   │
    │   ├── Providers/
    │   │   ├── Application/
    │   │   ├── Domain/
    │   │   ├── Infrastructure/
    │   │   └── Endpoints/
    │   │
    │   ├── Discovery/
    │   │
    │   ├── Recommendation/
    │   │
    │   ├── Search/
    │   │
    │   ├── Users/
    │   │
    │   ├── Favorites/
    │   │
    │   ├── Analytics/
    │   │
    │   ├── Developer/
    │   │
    │   ├── Advertising/
    │   │
    │   ├── Campaign/
    │   │
    │   └── Administration/
    │
    ├── BuildingBlocks/
    │   ├── Abstractions/
    │   ├── Authentication/
    │   ├── Authorization/
    │   ├── Caching/
    │   ├── Database/
    │   ├── Errors/
    │   ├── Logging/
    │   ├── Messaging/
    │   ├── Pagination/
    │   └── Observability/
    │
    ├── Infrastructure/
    │   ├── PostgreSQL/
    │   ├── Redis/
    │   ├── Meilisearch/
    │   ├── AI/
    │   ├── Storage/
    │   └── Providers/
    │
    ├── Tests/
    │   ├── Unit/
    │   ├── Integration/
    │   └── Architecture/
    │
    ├── Dockerfile
    └── README.md
```

---

# 🧩 Modules

## 1. Catalog

Responsible for the game catalog.

Responsibilities:

- Create game
- Update game
- Get game
- List games
- Categories
- Tags
- Game assets
- Game metadata
- Game status
- Game visibility

Example:

```text
Catalog
├── Games
├── Categories
├── Tags
├── Assets
└── Metadata
```

---

# 2. Providers

External game providers.

Example:

```text
GameMonetize
Provider B
Provider C
Direct Developers
Owned Games
```

Use an abstraction:

```csharp
public interface IGameProvider
{
    string Name { get; }

    Task<IReadOnlyCollection<ExternalGame>> GetGamesAsync(
        CancellationToken cancellationToken);

    Task<ExternalGame?> GetGameAsync(
        string externalId,
        CancellationToken cancellationToken);
}
```

Provider implementation:

```text
Providers
├── IGameProvider
├── GameMonetizeProvider
├── ProviderB
└── ProviderC
```

The application should never depend directly on a specific provider.

---

# 3. Discovery

Responsible for discovery feeds.

Examples:

```text
For You
Daily Discovery
Quick Play
Hidden Gems
Similar Games
Because You Played
Random Game
Collections
```

Example endpoint:

```http
GET /api/v1/discovery/for-you
```

---

# 4. Recommendation

Responsible for game recommendations.

Initial recommendation pipeline:

```text
User
 │
 ▼
User Preferences
 │
 ▼
Game History
 │
 ▼
Candidate Generation
 │
 ├── Similar Games
 ├── Popular Games
 ├── Trending Games
 ├── Fresh Games
 └── Category Games
 │
 ▼
Ranking
 │
 ▼
Personalized Recommendation
```

Initial ranking hypothesis:

```text
Similarity       30%
User Preference  20%
Popularity       20%
Engagement       15%
Freshness        10%
Diversity         5%
```

These values are experimental and must be recalibrated based on actual user behavior.

---

# 5. Search

Search is responsible for:

- Game title
- Description
- Genre
- Category
- Tags
- Developer
- Mechanics
- Mood
- Platform
- Player mode

Example:

```http
GET /api/v1/search/games?q=racing
```

Search flow:

```text
Client
  ↓
API
  ↓
Meilisearch
  ↓
Search Results
  ↓
Ranking / Filtering
  ↓
Response
```

For early MVP, PostgreSQL Full Text Search can be used before Meilisearch becomes necessary.

---

# 6. Users

Responsible for:

- Registration
- Login
- Profile
- Preferences
- Account settings
- User personalization

User preferences:

```text
Favorite Genres
Preferred Platform
Preferred Session Length
Multiplayer Preference
Language
```

---

# 7. Favorites

Responsible for:

- Add favorite
- Remove favorite
- List favorites
- Check favorite status

Endpoints:

```http
POST   /api/v1/users/me/favorites
DELETE /api/v1/users/me/favorites/{gameId}
GET    /api/v1/users/me/favorites
```

---

# 8. Analytics

Analytics is a critical module.

Events:

```text
game_impression
game_click
game_start
game_loaded
game_exit
game_complete
favorite_added
search
search_result_click
recommendation_impression
recommendation_click
share_game
category_click
```

Example:

```http
POST /api/v1/analytics/events
```

Payload:

```json
{
  "event": "game_start",
  "gameId": "game-123",
  "sessionId": "session-456",
  "source": "recommendation",
  "device": "mobile",
  "country": "ID"
}
```

---

# 9. Developer

Future developer platform.

Responsibilities:

```text
Developer Account
Game Submission
Game Management
Game Analytics
Game Promotion
Sponsored Discovery
Campaigns
API
```

---

# 10. Advertising

Advertising platform.

Responsibilities:

```text
Advertiser
Campaign
Targeting
Creative
Budget
Placement
Impression
Click
Conversion
Analytics
```

---

# 11. Administration

Internal management system.

Responsibilities:

```text
Dashboard
Games
Providers
Moderation
Categories
AI Enrichment
SEO
Featured Games
Campaigns
Users
Developers
Advertisers
Analytics
Revenue
Audit Logs
Settings
```

---

# 🔌 API Design

## Base URL

Development:

```text
https://localhost:7001
```

Staging:

```text
https://api-staging.gamediscoveries.com
```

Production:

```text
https://api.gamediscoveries.com
```

---

# API Versioning

Use URL-based versioning:

```text
/api/v1/...
```

Example:

```http
GET /api/v1/games
GET /api/v1/games/{slug}
GET /api/v1/discovery/for-you
GET /api/v1/trending
```

Future:

```text
/api/v2/...
```

---

# 📚 Main API Endpoints

## Health

```http
GET /health
GET /health/live
GET /health/ready
```

---

## Games

Implemented:

```http
GET /api/v1/games
GET /api/v1/games?page=1&pageSize=20&category=action&platform=mobile&search=mario&sort=popular&tag=arcade&mobileReady=true
GET /api/v1/games/{slug}
```

Planned:

```http
GET /api/v1/games/{id}/similar
GET /api/v1/games/{id}/related
```

---

## Categories

```http
GET /api/v1/categories
GET /api/v1/categories/{slug}/games
```

---

## Search

```http
GET /api/v1/search/games?q=racing
GET /api/v1/search/suggestions?q=car
```

---

## Discovery

Implemented:

```http
GET /api/v1/discoveries/home
```

Returns homepage sections from PostgreSQL (featured, trending, latest, popular, mobile, multiplayer).

Planned:

```http
GET /api/v1/discovery/for-you
GET /api/v1/discovery/daily
GET /api/v1/discovery/quick-play
GET /api/v1/discovery/hidden-gems
GET /api/v1/discovery/similar/{gameId}
GET /api/v1/discovery/random
```

---

## Administration (Game Feed Sync)

Protected by `X-GameDiscoveries-Admin-Key` (`GameFeedSync:AdminApiKey`).

```http
POST /api/v1/admin/game-feeds/sync
Content-Type: application/json
X-GameDiscoveries-Admin-Key: <admin-key>

{ "feedType": "Latest" }

POST /api/v1/admin/game-feeds/sync-all
X-GameDiscoveries-Admin-Key: <admin-key>
```

---

## Trending

```http
GET /api/v1/trending
GET /api/v1/trending/today
GET /api/v1/trending/weekly
GET /api/v1/trending/category/{slug}
```

---

## New Games

```http
GET /api/v1/games/new
GET /api/v1/games/new/today
GET /api/v1/games/new/weekly
```

---

## Mobile

```http
GET /api/v1/games/mobile
GET /api/v1/games/mobile/portrait
GET /api/v1/games/mobile/landscape
```

---

## Multiplayer

```http
GET /api/v1/multiplayer
GET /api/v1/multiplayer/2-player
GET /api/v1/multiplayer/co-op
GET /api/v1/multiplayer/competitive
```

---

## Users

```http
GET  /api/v1/users/me
PUT  /api/v1/users/me
GET  /api/v1/users/me/preferences
PUT  /api/v1/users/me/preferences
```

---

## Favorites

```http
GET    /api/v1/users/me/favorites
POST   /api/v1/users/me/favorites
DELETE /api/v1/users/me/favorites/{gameId}
```

---

## History

```http
GET  /api/v1/users/me/history
POST /api/v1/users/me/history
```

---

## Recommendations

```http
GET /api/v1/recommendations
GET /api/v1/recommendations/home
GET /api/v1/recommendations/game/{gameId}
```

---

## Analytics

```http
POST /api/v1/analytics/events
POST /api/v1/analytics/batch
```

---

# 📦 Response Format

Standard success response:

```json
{
  "success": true,
  "data": {},
  "error": null,
  "meta": null
}
```

List response:

```json
{
  "success": true,
  "data": [],
  "error": null,
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 1240,
    "totalPages": 62
  }
}
```

---

# ❌ Error Response

Use RFC-style Problem Details for HTTP errors.

Example:

```json
{
  "type": "https://api.gamediscoveries.com/errors/game-not-found",
  "title": "Game Not Found",
  "status": 404,
  "detail": "The requested game could not be found.",
  "instance": "/api/v1/games/super-car-racing",
  "traceId": "00-abc123"
}
```

Common HTTP status codes:

```text
200 OK
201 Created
204 No Content
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Entity
429 Too Many Requests
500 Internal Server Error
503 Service Unavailable
```

---

# 📄 Pagination

Use:

```text
?page=1&pageSize=20
```

Example:

```http
GET /api/v1/games?page=1&pageSize=20
```

Response:

```json
{
  "success": true,
  "data": [],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 500,
    "totalPages": 25
  }
}
```

Maximum page size:

```text
100
```

---

# 🔍 Filtering

Example:

```http
GET /api/v1/games
    ?category=racing
    &platform=mobile
    &players=2
    &sort=popular
```

Supported filters can include:

```text
category
genre
platform
players
mobile
multiplayer
difficulty
sessionLength
tag
sort
```

---

# 🔐 Authentication

Authentication uses:

```text
JWT
+
OAuth/OIDC
```

Example:

```http
Authorization: Bearer <access-token>
```

Public endpoints:

```text
GET /api/v1/games
GET /api/v1/search/games
GET /api/v1/trending
GET /api/v1/discovery/daily
```

Authenticated endpoints:

```text
GET /api/v1/users/me
GET /api/v1/users/me/favorites
GET /api/v1/recommendations
```

Admin endpoints require appropriate RBAC permissions.

---

# 👥 Roles

```text
Player
Developer
Advertiser
Moderator
Admin
SuperAdmin
```

Permission model:

```text
Role
 ↓
Permissions
 ↓
Module
 ↓
Endpoint
```

---

# 🗄️ Database Access

Use Dapper for data access.

Example:

```csharp
public sealed class GetGameBySlugQuery
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GetGameBySlugQuery(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<GameDto?> ExecuteAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id,
                slug,
                title,
                description,
                thumbnail_url
            FROM games
            WHERE slug = @Slug
            LIMIT 1;
            """;

        using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<GameDto>(
            sql,
            new { Slug = slug });
    }
}
```

Database queries should:

- Use parameterized SQL
- Avoid `SELECT *`
- Use appropriate indexes
- Avoid N+1 queries
- Support cancellation
- Return only required columns
- Be measurable through telemetry

---

# 🧠 Vertical Slice Pattern

Each feature should contain its own request, handler, validator, and response model.

Example:

```text
Catalog/
└── GetGameBySlug/
    ├── Endpoint.cs
    ├── Query.cs
    ├── Handler.cs
    ├── Validator.cs
    └── Response.cs
```

Instead of:

```text
Controllers/
Services/
Repositories/
DTOs/
Validators/
```

where unrelated functionality becomes tightly coupled.

---

# 🔄 Provider Integration

Game providers are isolated behind interfaces. **PostgreSQL is the source of truth** for the public API. Frontend clients never call GameMonetize directly.

```text
GameMonetize JSON Feed
        ↓
GameMonetizeClient (HttpClient + retry)
        ↓
GameMonetizeMapper → ExternalGame
        ↓
GameFeedImportService (idempotent upsert)
        ↓
PostgreSQL (games, categories, tags, feed memberships)
        ↓
Public Catalog / Discovery API → Next.js
```

Implemented GameMonetize pieces:

- `GameMonetizeOptions` + per-feed URLs (`GameMonetize:Feeds:*`) — no hardcoded production feed URLs in code
- `GameMonetizeClient.FetchFeedAsync(GameFeedType)`
- `GameFeedImportService` upsert by `(provider_id, provider_game_id)`
- `GameFeedSyncBackgroundService` scheduled sync (`GameFeedSync:*`)
- Admin manual sync endpoints

Deduplication key: `ExternalId/Source` via `game_provider_mappings`. If GameMonetize omits `id`, a deterministic SHA-256 id is derived from the game URL.

---

# 🤖 AI Enrichment Pipeline

Game ingestion pipeline:

```text
External Game
      ↓
Normalize Metadata
      ↓
Validate
      ↓
AI Enrichment
      ↓
Generate Tags
      ↓
Generate Description
      ↓
Generate SEO Metadata
      ↓
Generate Similarity Metadata
      ↓
Store PostgreSQL
      ↓
Index Meilisearch
```

AI output should always be treated as enrichment data rather than authoritative source data.

Original provider metadata must be preserved.

---

# 🧠 Recommendation Pipeline

```text
User Request
     ↓
Identify User
     ↓
Load Preferences
     ↓
Load History
     ↓
Candidate Generation
     ↓
Filtering
     ↓
Ranking
     ↓
Diversity
     ↓
Recommendation
     ↓
Analytics Event
```

Candidate sources:

```text
Similar Games
Trending
Popular
New
Hidden Gems
Category
Provider
Editorial Collections
```

---

# ⚡ Caching Strategy

Recommended cache layers:

```text
Browser
   ↓
Cloudflare
   ↓
Next.js
   ↓
API
   ↓
Redis
   ↓
PostgreSQL
```

Example TTL:

```text
Game detail       10 minutes
Trending           1–5 minutes
Popular            5 minutes
Categories         1 hour
Game metadata      1 hour
Recommendations    1–10 minutes
Search suggestions 1–5 minutes
```

TTL values are starting points and should be adjusted based on traffic and freshness requirements.

---

# 📊 Analytics Architecture

```text
Next.js
   │
   │ events
   ▼
GameDiscoveries API
   │
   ├── Validate
   ├── Enrich
   └── Normalize
   │
   ▼
Analytics Pipeline
   │
   ├── PostHog
   └── Internal Analytics Store
```

Important dimensions:

```text
User
Session
Game
Category
Source
Device
Platform
Country
Provider
Recommendation
Campaign
```

---

# 📝 Logging

Use **Serilog**.

Structured log example:

```json
{
  "timestamp": "2026-10-03T10:30:00Z",
  "level": "Information",
  "event": "GameStarted",
  "gameId": "123",
  "userId": "456",
  "source": "recommendation",
  "traceId": "abc123"
}
```

Never log:

```text
Passwords
JWT tokens
Refresh tokens
API secrets
Provider credentials
Payment credentials
Sensitive personal data
```

---

# 📡 Observability

Use:

```text
OpenTelemetry
      │
      ├── Metrics → Prometheus
      ├── Traces
      └── Logs → Loki
                    ↓
                 Grafana
```

Track:

```text
Request Rate
Error Rate
Latency
P95
P99
Database Latency
Redis Latency
Search Latency
Recommendation Latency
Provider Latency
Game Sync Duration
```

---

# 🩺 Health Checks

Required endpoints:

```http
GET /health
GET /health/live
GET /health/ready
```

Readiness should validate required dependencies:

```text
PostgreSQL
Redis
Meilisearch
```

External providers should not necessarily make the entire API unavailable when they are down.

---

# 🧪 Testing

Testing strategy:

```text
                Tests
                  │
        ┌─────────┼─────────┐
        ▼         ▼         ▼
      Unit   Integration  Architecture
```

## Unit Tests

Test:

- Business rules
- Recommendation scoring
- Validators
- Mapping
- Domain logic

## Integration Tests

Test:

- PostgreSQL
- Redis
- Meilisearch
- API endpoints
- Authentication
- Provider integration

## Architecture Tests

Ensure:

```text
Modules cannot access
unrelated internal implementation
directly.
```

Recommended tooling:

```text
xUnit
FluentAssertions
Testcontainers
NetArchTest
```

---

# 🐳 Docker

Example:

```bash
docker build \
  -t gamediscoveries-api:latest \
  .
```

Run:

```bash
docker run \
  -p 8080:8080 \
  gamediscoveries-api:latest
```

---

# 🐘 Local Infrastructure

Recommended Docker Compose services:

```yaml
services:

  postgres:
    image: postgres:18

  redis:
    image: redis:latest

  meilisearch:
    image: getmeili/meilisearch:latest

  api:
    build:
      context: .
      dockerfile: Dockerfile
```

---

# ⚙️ Configuration

Example:

```json
{
  "ConnectionStrings": {
    "Postgres": "",
    "Redis": "",
    "Meilisearch": ""
  },

  "Authentication": {
    "Authority": "",
    "Audience": ""
  },

  "AI": {
    "BaseUrl": "",
    "ApiKey": "",
    "Model": ""
  },

  "Storage": {
    "Endpoint": "",
    "Bucket": "",
    "AccessKey": "",
    "SecretKey": ""
  }
}
```

Secrets must be provided through environment variables or a secrets manager.

Never commit:

```text
API keys
Passwords
JWT secrets
Database passwords
Provider credentials
```

---

# 🔧 Environment Variables

Example:

```bash
Database__ConnectionString=
Redis__ConnectionString=
Meilisearch__Url=
Meilisearch__ApiKey=

GameMonetize__Enabled=true
GameMonetize__BaseUrl=https://gamemonetize.com
GameMonetize__Feeds__Latest=
GameMonetize__Feeds__Popular=
GameMonetize__Feeds__Action=
GameMonetize__Feeds__Puzzle=
GameMonetize__Feeds__Racing=
GameMonetize__Feeds__Sports=
GameMonetize__Feeds__Multiplayer=
GameMonetize__Feeds__Mobile=
GameMonetize__Feeds__TwoPlayer=
GameMonetize__Feeds__Featured=

GameFeedSync__Enabled=true
GameFeedSync__AdminApiKey=
GameFeedSync__LatestIntervalMinutes=60
GameFeedSync__PopularIntervalMinutes=120
GameFeedSync__CategoryIntervalMinutes=360

Authentication__Authority=
Authentication__Audience=
```

---

# 🚀 Getting Started

## Prerequisites

Install:

```text
.NET 10 SDK
Docker
Docker Compose
Git
```

Optional:

```text
PostgreSQL client
Redis CLI
Meilisearch
```

---

## Clone

```bash
git clone <repository-url>

cd gamediscoveries
```

---

## Start Infrastructure

```bash
docker compose up -d postgres redis meilisearch
```

Check:

```bash
docker compose ps
```

---

## Restore Dependencies

```bash
dotnet restore
```

---

## Configure Environment

Create:

```text
appsettings.Development.json
```

or configure environment variables.

---

## Run API

```bash
dotnet run \
  --project apps/api/GameDiscoveries.Api
```

---

# 🌐 Local URLs

Example:

```text
API
https://localhost:7001

HTTP
http://localhost:5001

OpenAPI
/openapi/v1.json

Health
/health
```

---

# 📖 OpenAPI

OpenAPI is the source of truth for the API contract.

Development:

```text
/openapi/v1.json
```

The API should expose documentation for:

- Endpoints
- Parameters
- Request models
- Response models
- Authentication
- HTTP status codes
- Error responses

Interactive Swagger UI can be enabled for development environments if desired.

---

# 🔀 Git Workflow

Recommended:

```text
main
  │
  ├── develop
  │
  ├── feature/*
  ├── fix/*
  ├── refactor/*
  └── hotfix/*
```

Feature example:

```bash
git checkout develop

git checkout -b feature/game-recommendation
```

---

# 📝 Commit Convention

Use Conventional Commits:

```text
feat: add game recommendation endpoint
fix: fix game provider synchronization
refactor: improve catalog query
perf: optimize trending query
test: add recommendation tests
docs: update API documentation
chore: update dependencies
```

---

# 🔄 CI/CD Pipeline

Recommended pipeline:

```text
Git Push
   ↓
Restore
   ↓
Build
   ↓
Unit Tests
   ↓
Integration Tests
   ↓
Architecture Tests
   ↓
Code Quality
   ↓
Security Scan
   ↓
Docker Build
   ↓
Container Scan
   ↓
Push Image
   ↓
Deploy Staging
   ↓
Smoke Test
   ↓
Production Approval
   ↓
Deploy Production
```

---

# 📦 API Docker Image

Recommended naming:

```text
registry.example.com/gamediscoveries/api
```

Tags:

```text
latest
1.0.0
1.0.0-abc123
```

Production deployments should preferably use immutable version tags instead of relying on `latest`.

---

# 🔐 Security Guidelines

The API must:

- Validate all input
- Use parameterized SQL
- Apply authentication where required
- Apply RBAC
- Rate-limit public endpoints
- Protect provider credentials
- Protect AI credentials
- Avoid sensitive logs
- Validate uploaded assets
- Sanitize external metadata
- Restrict administrative endpoints
- Implement CORS deliberately
- Use HTTPS
- Use secure HTTP headers

Game provider content should be treated as untrusted external input.

---

# 🚦 Rate Limiting

Recommended initial limits:

```text
Public API
60 requests/minute/IP

Search
30 requests/minute/IP

Authentication
10 requests/minute/IP

Analytics
Higher throughput with batching

Admin
Role-based limits
```

Actual limits should be tuned based on production traffic.

---

# 🧹 Background Jobs

Background processing will eventually handle:

```text
Game synchronization
Game metadata enrichment
AI enrichment
Search indexing
Recommendation refresh
Trending calculation
Analytics aggregation
Broken game detection
SEO generation
Campaign processing
```

Initial implementation can use hosted background workers.

As scale increases, introduce a dedicated queue/message broker.

---

# 🔮 Future Messaging

Possible future technology:

```text
RabbitMQ
Kafka
Azure Service Bus
AWS SQS
```

Do not introduce a message broker until asynchronous workload justifies the additional operational complexity.

---

# 🌎 Globalization

The API should support:

```text
Language
Country
Currency
Timezone
Platform
Regional content
```

Initial languages:

```text
English
Indonesian
```

Future:

```text
Spanish
Portuguese
French
German
Japanese
Korean
```

---

# 📈 Scalability Strategy

Initial:

```text
1 API
1 PostgreSQL
1 Redis
1 Meilisearch
```

Scale API horizontally:

```text
           Load Balancer
                │
      ┌─────────┼─────────┐
      ▼         ▼         ▼
    API 1     API 2     API 3
      │         │         │
      └─────────┼─────────┘
                ▼
          PostgreSQL
                │
              Redis
```

The API should remain stateless wherever practical.

---

# 🧠 Recommendation Evolution

## Phase 1

Rule-based:

```text
Tags
Categories
Popularity
Freshness
History
Preferences
```

## Phase 2

Behavioral:

```text
CTR
Game Starts
Session Duration
Completion
Favorites
Repeat Plays
```

## Phase 3

Machine Learning:

```text
Embeddings
Collaborative Filtering
Learning-to-Rank
User Vectors
Game Vectors
```

## Phase 4

AI Discovery:

```text
Natural Language Intent
        ↓
Intent Understanding
        ↓
Candidate Retrieval
        ↓
ML Ranking
        ↓
LLM Explanation
        ↓
Recommendation
```

The LLM should primarily improve **understanding and explanation**, while deterministic/ML systems remain responsible for scalable retrieval and ranking.

---

# 🎯 API Success Metrics

Technical:

```text
Availability
P95 Latency
P99 Latency
Error Rate
Database Latency
Redis Hit Ratio
Search Latency
Recommendation Latency
Provider Sync Success Rate
```

Product:

```text
Game Start Rate
Recommendation CTR
Second Game Rate
Games / Session
Favorites / User
Return Sessions
Search → Play Conversion
```

---

# ⭐ North Star Metric

Primary product metric:

```text
Game Sessions per Monthly Active User
```

Supporting metrics:

```text
Games Started
Games per Session
Second Game Start Rate
Recommendation CTR
7-Day Return Rate
30-Day Return Rate
```

---

# 🛣️ Roadmap

## Phase 1 — Foundation

```text
.NET API
PostgreSQL
Authentication
Catalog
Provider Integration
Docker
CI/CD
Health Checks
OpenAPI
```

## Phase 2 — Discovery

```text
Search
Trending
New Games
Categories
Discovery
Recommendations
Analytics
```

## Phase 3 — AI

```text
AI Game Enrichment
Natural Language Discovery
Game Similarity
Recommendation Explanation
```

## Phase 4 — Growth

```text
Favorites
Personalization
Collections
Community
Challenges
Achievements
```

## Phase 5 — Monetization

```text
Advertising
Sponsored Games
Developer Promotion
Campaign Management
Revenue Analytics
```

## Phase 6 — Platform

```text
Developer Portal
Public API
Advanced Recommendation
ML Ranking
Multi-provider Ecosystem
Global Scaling
```

---

# 🧭 Development Principles

## 1. Discovery First

The API exists to help users discover games, not only retrieve games.

## 2. API First

All business capabilities should be exposed through well-defined application interfaces.

## 3. SEO First

Game metadata must support SEO-friendly web experiences.

## 4. Analytics First

Important user interactions must generate measurable events.

## 5. Provider Agnostic

Never couple core business logic directly to one game provider.

## 6. Database as Source of Truth

PostgreSQL remains authoritative.

## 7. Cache is Disposable

Redis and search indexes must be rebuildable.

## 8. AI as Intelligence Layer

AI enriches the platform but should not become an uncontrolled source of truth.

## 9. Avoid Premature Microservices

Start modular. Extract services when there is a measurable need.

## 10. Security by Default

Authentication, authorization, validation, rate limiting, logging, and secrets management are part of the architecture, not later additions.

---

# 🤝 Contribution

1. Create a feature branch.

```bash
git checkout -b feature/my-feature
```

2. Implement the feature.

3. Add tests.

4. Run:

```bash
dotnet build
dotnet test
```

5. Run formatting/analyzers.

6. Commit using Conventional Commits.

```bash
git commit -m "feat: add game discovery endpoint"
```

7. Create a Merge Request.

---

# 📄 License

Copyright © GameDiscoveries.

The final license model should be defined before public repository distribution.

---

# 🧩 Related Projects

```text
GameDiscoveries
│
├── Web
│   └── Next.js 16
│
├── API
│   └── .NET 10
│
├── Infrastructure
│   ├── Docker
│   ├── Cloudflare
│   ├── PostgreSQL
│   ├── Redis
│   └── Meilisearch
│
├── AI
│   └── Game Intelligence
│
├── Analytics
│   └── PostHog
│
└── Developer Platform
    └── Future
```

---

# 🚀 Final Architecture

```text
                         GAME DISCOVERIES
                                │
                                ▼
                         Next.js 16 Web
                                │
                                ▼
                     ┌────────────────────┐
                     │   .NET 10 API      │
                     │                    │
                     │ Modular Monolith   │
                     │ Vertical Slice     │
                     └─────────┬──────────┘
                               │
            ┌──────────────────┼──────────────────┐
            │                  │                  │
            ▼                  ▼                  ▼
       PostgreSQL            Redis           Meilisearch
       System of Record       Cache             Search
            │
            ▼
      Game Intelligence
            │
      ┌─────┼─────┐
      ▼     ▼     ▼
     AI   Events  Recommendation
      │     │        Engine
      └─────┼────────┘
            ▼
        Discovery
            │
            ▼
      Discover → Play
            ↓
      Recommend → Play Again
            ↓
          Return
```

---

## 🏆 Architecture Principle

> **Build the simplest architecture that can support the discovery engine today, while keeping clear module boundaries so high-scale components can be extracted later.**

GameDiscoveries API is therefore optimized for:

**Performance + SEO + Discovery + Analytics + Personalization + Scalability**
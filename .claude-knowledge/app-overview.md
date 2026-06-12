# App Overview

C#/.NET rewrite of the Movie Night Picker backend (originally TypeScript Express/Apollo GraphQL). This describes the **target** design — none of it is built yet (repo is a shell).

## Architecture (target)

ASP.NET Core Web API. Request flow:

```
client → API endpoint (controller / minimal API)
       → application/service layer (suggestion + filter logic, LINQ)
       → TMDB typed HttpClient   (external movie data)
       → EF Core DbContext       (user data: collections, ratings, reviews)
```

REST endpoints replace the original's GraphQL schema. (GraphQL in .NET via HotChocolate is a possible later variant, but REST first — it's the closer analog to the Epicor REST work the learning is aimed at.)

## Data Model (target — EF Core entities)

Ported from the original's 8 Prisma models. User data only; movie data is fetched live from TMDB and referenced by `tmdbId` (not stored in full).

- `User` — auth identity
- `Collection` — user-scoped movie lists (+ insights/analytics)
- `CollectionItem` — movie membership in a collection (stores `tmdbId`)
- `Rating` — 1–10 score per movie, per user
- `Review` — written review per movie, per user

Confirm exact fields against the original Prisma schema when scaffolding the Data project.

## Core business logic to port

The differentiators — port these faithfully, they're the LINQ-heavy payoff:

- **Suggest flow** — 10-round preference discovery (genre → era → mood → popularity cycling) → recommendation via a **5-strategy cascade** with quality floors, fallback chains, dedup, exclusion rules.
- **Shuffle** — random discovery with **15+ filters** (genre, year, cast, crew, keywords, streaming providers, popularity, …).
- **Collection insights** — aggregation (genre/actor/keyword counts, stats).

## TMDB integration

External REST API, wrapped in a typed `HttpClient`. This is the rehearsal for wrapping Epicor REST/BAQ endpoints on the job. Needs a TMDB API key (env / user-secrets).

## Auth Flow (target)

ASP.NET Core JWT bearer auth — mirrors the original's JWT setup. Register/login issues a JWT; `[Authorize]` on user-scoped endpoints; current user resolved from claims.

## Key Files

_TBD — populate at scaffold time once the solution layout is real._

| Concern | Files |
|---|---|
| Entry point | (TBD — `Program.cs`) |
| Auth | |
| Database / EF Core | |
| API endpoints | |
| TMDB client | |
| Suggestion logic | |

## Environment Variables

- `TMDB__ApiKey` — TMDB API key
- `ConnectionStrings__Default` — PostgreSQL connection string
- `Jwt__*` — signing key / issuer / audience

(Real values in user-secrets or env, never committed.)

## Reference

- Original app: `~/brain/projects/movie-night-picker/overview.md`
- Learning plan: `~/brain/knowledge/csharp/study-plan.md`

# Work Queue

Prioritized work. Also the input for the swarm planner. **Current state: repo shell, no code.**

## Priority Order

1. **Phase 0 — scaffold the solution (SOLO agent, no swarm)** — nothing to parallelize until code exists.
2. **Phase 1 — TMDB client + read endpoints** (swarm-ready)
3. **Phase 2 — EF Core + user data** (collections, ratings, reviews)
4. **Phase 3 — suggest flow + shuffle filters** (the LINQ payoff)
5. **Phase 4 — auth, tests, polish**

## Phase 0 — Scaffold (do first, solo)

- [ ] `dotnet new sln -n MovieNightPicker`
- [ ] Create projects: `MovieNightPicker.Api` (webapi), `.Core`, `.Data`, `.Tmdb`, and `tests/MovieNightPicker.Tests` (xUnit). Add all to the solution.
- [ ] Enable nullable reference types + treat-warnings sensibly across projects.
- [ ] `dotnet build` green; commit. Fill in `app-overview.md` Key Files table.

Once Phase 0 is committed, the swarm can take over Phases 1+ (work sets below have no file overlap).

## Work Sets (Phase 1+ — swarm)

### Set 1: TMDB client (`MovieNightPicker.Tmdb`)
**Files:** `src/MovieNightPicker.Tmdb/**`
- [ ] Typed `HttpClient` wrapper over TMDB (search, discover, movie/person detail, credits)
- [ ] DTOs + `System.Text.Json` mapping
- [ ] Error handling + rate-limit awareness

### Set 2: Data layer (`MovieNightPicker.Data`)
**Files:** `src/MovieNightPicker.Data/**`
- [ ] EF Core `DbContext` + entities (User, Collection, CollectionItem, Rating, Review)
- [ ] Initial migration
- [ ] Npgsql / PostgreSQL wiring (connection string via config)
- ⚠️ Migrations are **sequential** — only one worker touches the DbContext per wave.

### Set 3: API surface (`MovieNightPicker.Api`)
**Files:** `src/MovieNightPicker.Api/**`
- [ ] Program.cs: DI, config, middleware, register typed TMDB client + DbContext
- [ ] Read endpoints: search, discover/shuffle, movie/person detail
- [ ] JWT auth + user-scoped endpoints (collections, ratings, reviews)

### Set 4: Suggestion engine (`MovieNightPicker.Core`)
**Files:** `src/MovieNightPicker.Core/**`
- [ ] 10-round suggest flow + 5-strategy recommendation cascade (LINQ)
- [ ] 15+ shuffle filters
- [ ] Collection insights aggregation

## Parallelism Matrix

|        | Set 1 | Set 2 | Set 3 | Set 4 |
|--------|-------|-------|-------|-------|
| Set 1  | —     | Yes   | Yes*  | Yes   |
| Set 2  |       | —     | Yes*  | Yes   |
| Set 3  |       |       | —     | Yes*  |
| Set 4  |       |       |       | —     |

\* Set 3 depends on the interfaces from Sets 1, 2, 4 — sequence a thin contract/stub first, then parallelize the implementations.

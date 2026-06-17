# Work Queue

Prioritized work. Also the input for the swarm planner. **Current state: repo shell, no code.**

## Priority Order

1. **Phase 0 — scaffold the solution (SOLO agent, no swarm)** — nothing to parallelize until code exists.
2. **Phase 1 — TMDB client + read endpoints** (swarm-ready)
3. **Phase 2 — EF Core + user data** (collections, ratings, reviews)
4. **Phase 3 — suggest flow + shuffle filters** (the LINQ payoff)
5. **Phase 4 — auth, tests, polish**

## Phase 0 — Scaffold (DONE ✅, solo)

- [x] Solution created — `MovieNightPicker.slnx` (.NET 10 defaulted to the XML format)
- [x] Projects created: `.Api` (webapi), `.Core`, `.Data`, `.Tmdb`, `tests/MovieNightPicker.Tests` (xUnit), all added to the solution
- [x] Nullable + implicit usings + lang `latest` centralized in root `Directory.Build.props`
- [x] `dotnet build` green (0 warnings), `dotnet test` green, `dotnet format` clean. Key Files table filled in `app-overview.md`.

**Note:** `dotnet` is installed user-locally at `~/.dotnet` (SDK 10.0.301); PATH is set in `~/.bashrc`. Swarm worktrees inherit this PATH.

Phase 0 is committed — the swarm can now take over Phases 1+ (work sets below have no file overlap).

## Work Sets (Phase 1+ — swarm)

### Set 1: TMDB client (`MovieNightPicker.Tmdb`) — ✅ DONE (Wave 1, 2026-06-17)
**Files:** `src/MovieNightPicker.Tmdb/**`
- [x] Typed `HttpClient` wrapper over TMDB (search, discover, movie/person detail, credits) — task-002
- [x] DTOs + `System.Text.Json` mapping + `TmdbQueryStringBuilder` — task-001
- [x] Error handling (`TmdbApiException`) — task-002
- [ ] Rate-limit awareness / caching (deferred — see original's TTL cache + request dedup)

### Set 2: Data layer (`MovieNightPicker.Data`) — ✅ DONE (Wave 1, 2026-06-17)
**Files:** `src/MovieNightPicker.Data/**`
- [x] EF Core `DbContext` + all 8 entities (User, MovieHistory, SavedMovie, Rating, Review, Collection, CollectionMovie, SuggestMovieHistory) — task-003
- [x] Initial migration (`InitialCreate`) — task-004
- [x] Npgsql / PostgreSQL wiring + design-time factory (`AddData(connectionString)`) — task-004

### Set 3: API surface (`MovieNightPicker.Api`) — ⏭️ NEXT WAVE
**Files:** `src/MovieNightPicker.Api/**`
- [ ] Program.cs: DI, config, middleware, register typed TMDB client + DbContext
- [ ] **TMDB→Core adapter**: implement `IMovieDataSource` over `ITmdbClient` (this is the integration seam Wave 1 deliberately left open)
- [ ] Read endpoints: search, discover/shuffle, movie/person detail
- [ ] JWT auth + user-scoped endpoints (collections, ratings, reviews)

### Set 4: Suggestion engine (`MovieNightPicker.Core`) — 🟡 PARTIAL (Wave 1)
**Files:** `src/MovieNightPicker.Core/**`
- [x] Domain models + constants + `IMovieDataSource` abstraction — task-005
- [x] 5-strategy recommendation cascade + preference extraction (LINQ) — task-007
- [x] 15+ shuffle filters + progressive fallback chain (LINQ) — task-006
- [ ] **10-round suggest flow** (deferred to Wave 2)
- [ ] **Collection insights aggregation** (deferred to Wave 2)

## Parallelism Matrix

|        | Set 1 | Set 2 | Set 3 | Set 4 |
|--------|-------|-------|-------|-------|
| Set 1  | —     | Yes   | Yes*  | Yes   |
| Set 2  |       | —     | Yes*  | Yes   |
| Set 3  |       |       | —     | Yes*  |
| Set 4  |       |       |       | —     |

\* Set 3 depends on the interfaces from Sets 1, 2, 4 — sequence a thin contract/stub first, then parallelize the implementations.

## Follow-ups (from reviewer)

### Set 4: Suggestion engine
- [ ] `DiscoverParams.GetHashCode()` omits `ExcludeGenres`/`ExcludeCast`/`ExcludeCrew` (task-005), though `Equals` compares them. Harmless today (equal objects still hash equal; only risks extra collisions in dictionaries/dedup keyed on params that differ only by exclude lists). Include them for symmetry when convenient.

### Set 1: TMDB typed client
- [ ] `TmdbQueryStringBuilder` (task-001): `ForDiscover` and `ForOptions` can both emit the same TMDB keys (`vote_average.gte`, `vote_count.gte`, `popularity.gte/lte`, `with_watch_providers`). `BuildDiscoverQuery` concatenates both, so a caller that sets these on *both* `DiscoverParams` and `TmdbRequestOptions` produces duplicate query keys. No current caller hits this; decide precedence (discover wins?) when wiring TmdbClient/the Core adapter.

### Set 2: Data layer
- [ ] `Rating.RatingValue` is documented as 1-10 but has no DB-level check constraint (task-003). Add a `HasCheckConstraint`/range validation (in the API DTO validators) when wiring the ratings endpoint so out-of-range values can't be persisted.

### Set 4: Suggestion engine (cont.)
- [ ] `PreferenceExtractor.Extract(IReadOnlyList<Movie>)` can only derive genres + year range; keywords/actors/crew need TMDB credit/keyword data (task-007 added a `SelectedMovie` enriched overload + TODO). When wiring the suggest endpoint, build `SelectedMovie`s from TMDB credits/keywords so the full preference profile is used.

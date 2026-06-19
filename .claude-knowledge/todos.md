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
- [x] Rate-limit awareness + in-memory TTL caching + request dedup (`CachingTmdbClient`) — Wave 2, task-005

### Set 2: Data layer (`MovieNightPicker.Data`) — ✅ DONE (Wave 1, 2026-06-17)
**Files:** `src/MovieNightPicker.Data/**`
- [x] EF Core `DbContext` + all 8 entities (User, MovieHistory, SavedMovie, Rating, Review, Collection, CollectionMovie, SuggestMovieHistory) — task-003
- [x] Initial migration (`InitialCreate`) — task-004
- [x] Npgsql / PostgreSQL wiring + design-time factory (`AddData(connectionString)`) — task-004

### Set 3: API surface (`MovieNightPicker.Api`) — 🟡 PARTIAL (Wave 2)
**Files:** `src/MovieNightPicker.Api/**`
- [x] Program.cs: DI, config, middleware (`AddAppServices`), ProblemDetails error handling — Wave 2, task-003
- [x] **TMDB→Core adapter**: `TmdbMovieDataSource : IMovieDataSource` over `ITmdbClient` — Wave 2, task-003
- [x] Read endpoints: search, discover/shuffle, movie/person detail, `POST /movies/suggest` — Wave 2, task-004
- [ ] JWT auth + user-scoped endpoints (collections, ratings, reviews) — ⏭️ Wave 3
- [ ] `GET /suggest/round/{n}` (10-round flow) + `GET /collections/{id}/insights` endpoints — ⏭️ Wave 3 (Core logic exists, no HTTP surface yet)

### Set 4: Suggestion engine (`MovieNightPicker.Core`) — ✅ DONE (Waves 1–2)
**Files:** `src/MovieNightPicker.Core/**`
- [x] Domain models + constants + `IMovieDataSource` abstraction — W1 task-005
- [x] 5-strategy recommendation cascade + preference extraction (LINQ) — W1 task-007
- [x] 15+ shuffle filters + progressive fallback chain (LINQ) — W1 task-006
- [x] 10-round suggest flow (`SuggestFlow` + `SuggestRoundGenerator`) — W2 task-001
- [x] Collection insights aggregation (`CollectionInsights`) — W2 task-002
- ℹ️ Both 10-round flow and insights need HTTP endpoints (see Set 3, Wave 3)

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
- [ ] `TmdbQueryStringBuilder` (task-001): `ForDiscover` and `ForOptions` can both emit the same TMDB keys (`vote_average.gte`, `vote_count.gte`, `popularity.gte/lte`, `with_watch_providers`). `BuildDiscoverQuery` concatenates both, so a caller that sets these on *both* `DiscoverParams` and `TmdbRequestOptions` produces duplicate query keys. No current caller hits this; decide precedence (discover wins?) when wiring TmdbClient/the Core adapter. — ✅ RESOLVED (task-005: discover wins, keys de-duplicated).
- [ ] Captive dependency (task-005): `AddTmdbClient` now registers `CachingTmdbClient` as a **singleton** that captures the transient typed `TmdbClient` (and thus one pooled `HttpClient`) for the app lifetime, defeating `HttpClientFactory` handler rotation (DNS/socket refresh). Harmless for this single-user app; if it ever runs long-lived against changing infra, consider making the decorator resolve the inner client per-call or use `IHttpClientFactory` directly. The decorator itself must stay singleton (shared cache + in-flight dedup dictionary).

### Set 2: Data layer
- [x] `Rating.RatingValue` is documented as 1-10 but has no DB-level check constraint (task-003). — ✅ RESOLVED (task-006: CK_Rating_RatingValue_Range constraint + migration). Still worth adding API DTO validation when wiring the ratings endpoint for a friendlier 400 than a DB constraint violation.

### Set 4: Suggestion engine (cont.)
- [x] `PreferenceExtractor.Extract(IReadOnlyList<Movie>)` can only derive genres + year range; keywords/actors/crew need TMDB credit/keyword data (task-007 added a `SelectedMovie` enriched overload + TODO). When wiring the suggest endpoint, build `SelectedMovie`s from TMDB credits/keywords so the full preference profile is used. — ✅ RESOLVED (task-004: POST /movies/suggest enriches picks via TMDB credits + keywords).

### Wave 2 follow-ups (from reviewer)
- [x] `SuggestFlow.GetRoundAsync` (task-001) is implemented in Core but not yet exposed via an HTTP endpoint — no `GET/POST /suggest/round/{n}` surface wires it up. — ✅ RESOLVED (Wave 3 task-005: `POST /suggest/round/{n}` drives the flow, round 1-10 validated; selected ids passed in the body, stateless).
- [x] `CollectionInsights.Compute` (task-002) is pure Core with no HTTP surface yet. — ✅ RESOLVED (Wave 3 task-006: `GET /collections/{id}/insights` builds `InsightsMovie`s from TMDB credits/keywords for the owner's collection).
- [x] The suggest endpoint (task-004) makes 3 sequential TMDB calls per selected id (GetMovie + GetMovieCredits + GetMovieKeywords). — ✅ RESOLVED (Wave 3 task-005: per-pick enrichment parallelized via `Task.WhenAll`, order + skip-on-null preserved).

### Wave 3 follow-ups (from reviewer)
- [ ] No brute-force / rate-limiting protection on `POST /auth/login` (task-001). Single-user app so low risk, but add ASP.NET rate limiting on the auth group if it ever faces the public internet.
- [ ] No JWT refresh-token / logout / revocation mechanism (task-001/002) — tokens are valid until expiry (60 min). Fine for the learning project; revisit if sessions need invalidation.
- [ ] `PasswordHasher.Verify` (task-001) reads the stored iteration count, so it survives a parameter bump — but there's no rehash-on-successful-login path to migrate old hashes to a stronger cost. Add one if `Iterations` is ever raised.
- [ ] TMDB→Core `Movie` mapping is now duplicated: `Adapters/TmdbMovieDataSource` and `Services/InsightsService.ToMovie` (task-006) both map the same DTO. Extract a shared mapper (e.g. a `TmdbMovie.ToCore()` extension) to keep them from drifting.
- [ ] Rating value validation is doubled (task-004): `UpsertRatingRequest` carries a `[Range(1,10)]` attribute *and* the handler does a manual `body.Value is < 1 or > 10` check. Minimal APIs don't auto-run DataAnnotations without a validation filter, so the attribute is currently decorative — the manual check is what enforces it. Either add a global validation filter (and drop the manual checks across handlers) or remove the now-misleading attributes.

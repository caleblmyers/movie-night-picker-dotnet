# Work Queue

Prioritized work. Also the input for the swarm planner. **Current state: full-stack feature-complete + polished (Waves 0–5, 2026-06-19).** Backend + Blazor WASM frontend done and pushed to GitHub (public: caleblmyers/movie-night-picker-dotnet); 184 tests green, app boots (API `/health` ok + auth 401s, Blazor host serves). Wave 5 cleared the reviewer follow-ups (auth rate-limit + rehash, validation filter, shared TMDB mapper, caching captive-dependency, web error handling + UX). Remaining: a few small open follow-ups (below) + future directions (deployment, JWT refresh/revocation if ever needed).

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

### Set 3: API surface (`MovieNightPicker.Api`) — ✅ DONE (Waves 2–3)
**Files:** `src/MovieNightPicker.Api/**`
- [x] Program.cs: DI, config, middleware (`AddAppServices`), ProblemDetails error handling — W2 task-003
- [x] **TMDB→Core adapter**: `TmdbMovieDataSource : IMovieDataSource` over `ITmdbClient` — W2 task-003
- [x] Read endpoints: search, discover/shuffle, movie/person detail, `POST /movies/suggest` — W2 task-004
- [x] JWT auth (register/login, bearer) + user-scoped endpoints (collections, ratings, reviews) — W3 task-001/003/004/002
- [x] `POST /suggest/round/{n}` (10-round flow) + `GET /collections/{id}/insights` endpoints — W3 task-005/006

### Set 4: Suggestion engine (`MovieNightPicker.Core`) — ✅ DONE (Waves 1–2)
**Files:** `src/MovieNightPicker.Core/**`
- [x] Domain models + constants + `IMovieDataSource` abstraction — W1 task-005
- [x] 5-strategy recommendation cascade + preference extraction (LINQ) — W1 task-007
- [x] 15+ shuffle filters + progressive fallback chain (LINQ) — W1 task-006
- [x] 10-round suggest flow (`SuggestFlow` + `SuggestRoundGenerator`) — W2 task-001
- [x] Collection insights aggregation (`CollectionInsights`) — W2 task-002
- ℹ️ Both 10-round flow and insights need HTTP endpoints (see Set 3, Wave 3)

### Set 5: Web UI (`MovieNightPicker.Web`, Blazor WASM) — ✅ DONE (Wave 4, 2026-06-18)
**Files:** `src/MovieNightPicker.Web/**`
- [x] Foundation (solo): WASM project, auth plumbing (localStorage token, JWT auth-state, bearer handler, login/register), HttpClient→API, CORS on API, shared `MovieSummary`/`MovieCard` + NavLinks
- [x] Browse: `MovieApiClient`, Search, Shuffle, Movie detail — W4 task-001/002
- [x] Suggest: `SuggestApiClient`, quick-suggest + 10-round flow — W4 task-003/004
- [x] Library: collections (+ insights) and ratings/reviews (+ `RatingStars`) — W4 task-005/006

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
- [x] Captive dependency (task-005): `AddTmdbClient` now registers `CachingTmdbClient` as a **singleton** that captures the transient typed `TmdbClient` (and thus one pooled `HttpClient`) for the app lifetime, defeating `HttpClientFactory` handler rotation (DNS/socket refresh). Harmless for this single-user app; if it ever runs long-lived against changing infra, consider making the decorator resolve the inner client per-call or use `IHttpClientFactory` directly. The decorator itself must stay singleton (shared cache + in-flight dedup dictionary). — ✅ RESOLVED (Wave 5 task-006: decorator takes a `Func<ITmdbClient>` and resolves a fresh `TmdbClient` per fetch; stays singleton. `TmdbClient` is not `IDisposable`, so root-provider transient resolution doesn't leak).

### Set 2: Data layer
- [x] `Rating.RatingValue` is documented as 1-10 but has no DB-level check constraint (task-003). — ✅ RESOLVED (task-006: CK_Rating_RatingValue_Range constraint + migration). Still worth adding API DTO validation when wiring the ratings endpoint for a friendlier 400 than a DB constraint violation.

### Set 4: Suggestion engine (cont.)
- [x] `PreferenceExtractor.Extract(IReadOnlyList<Movie>)` can only derive genres + year range; keywords/actors/crew need TMDB credit/keyword data (task-007 added a `SelectedMovie` enriched overload + TODO). When wiring the suggest endpoint, build `SelectedMovie`s from TMDB credits/keywords so the full preference profile is used. — ✅ RESOLVED (task-004: POST /movies/suggest enriches picks via TMDB credits + keywords).

### Wave 2 follow-ups (from reviewer)
- [x] `SuggestFlow.GetRoundAsync` (task-001) is implemented in Core but not yet exposed via an HTTP endpoint — no `GET/POST /suggest/round/{n}` surface wires it up. — ✅ RESOLVED (Wave 3 task-005: `POST /suggest/round/{n}` drives the flow, round 1-10 validated; selected ids passed in the body, stateless).
- [x] `CollectionInsights.Compute` (task-002) is pure Core with no HTTP surface yet. — ✅ RESOLVED (Wave 3 task-006: `GET /collections/{id}/insights` builds `InsightsMovie`s from TMDB credits/keywords for the owner's collection).
- [x] The suggest endpoint (task-004) makes 3 sequential TMDB calls per selected id (GetMovie + GetMovieCredits + GetMovieKeywords). — ✅ RESOLVED (Wave 3 task-005: per-pick enrichment parallelized via `Task.WhenAll`, order + skip-on-null preserved).

### Wave 3 follow-ups (from reviewer)
- [x] No brute-force / rate-limiting protection on `POST /auth/login` (task-001). Single-user app so low risk, but add ASP.NET rate limiting on the auth group if it ever faces the public internet. — ✅ RESOLVED (Wave 5 task-003: fixed-window limiter ~10 req/min per client IP on the `/auth` group, 429 over limit).
- [ ] No JWT refresh-token / logout / revocation mechanism (task-001/002) — tokens are valid until expiry (60 min). Fine for the learning project; revisit if sessions need invalidation.
- [x] `PasswordHasher.Verify` (task-001) reads the stored iteration count, so it survives a parameter bump — but there's no rehash-on-successful-login path to migrate old hashes to a stronger cost. Add one if `Iterations` is ever raised. — ✅ RESOLVED (Wave 5 task-003: `NeedsRehash` + transparent re-hash/persist on successful login).
- [x] TMDB→Core `Movie` mapping is now duplicated: `Adapters/TmdbMovieDataSource` and `Services/InsightsService.ToMovie` (task-006) both map the same DTO. Extract a shared mapper (e.g. a `TmdbMovie.ToCore()` extension) to keep them from drifting. — ✅ RESOLVED (Wave 5 task-005: single `TmdbMovie.ToCore()` extension in `MovieNightPicker.Tmdb.Dtos`; both callers use it).
- [x] Rating value validation is doubled (task-004): `UpsertRatingRequest` carries a `[Range(1,10)]` attribute *and* the handler does a manual `body.Value is < 1 or > 10` check. Minimal APIs don't auto-run DataAnnotations without a validation filter, so the attribute is currently decorative — the manual check is what enforces it. Either add a global validation filter (and drop the manual checks across handlers) or remove the now-misleading attributes. — ✅ RESOLVED (Wave 5 task-004: reusable `ValidationEndpointFilter<T>` + `.WithRequestValidation<T>()` makes the DataAnnotations real; manual range/content checks removed from both upsert handlers). Other DTO-bearing endpoints can now adopt `.WithRequestValidation<T>()` for the same one-liner enforcement — a natural follow-up if more validated request bodies appear.

### Wave 4 follow-ups (from reviewer — Blazor web UI)
- [x] Web API calls have no error handling (task-003 `Suggest.razor` / `SuggestApiClient`): `Http.GetFromJsonAsync` and the suggest calls throw on a non-success / network failure, surfacing as an unhandled Blazor exception rather than a friendly message. Consider a shared try/catch or an error-display helper across the web pages so transient API failures show a toast/banner instead of breaking the page. — ✅ RESOLVED for the suggest pages (Wave 5 task-001: reusable `ApiCall.RunAsync`/`ApiResult<T>` helper + dismissible `ApiErrorBoundary` banner, wired into `Suggest.razor` + `SuggestRounds.razor`).
- [x] `CollectionDetail.razor` (task-005) loads member movies one-by-one in a sequential `foreach` (N+1 `GET /movies/{id}`). — ✅ RESOLVED (Wave 5 task-002: parallel `Task.WhenAll` fetch, order + skip-on-null preserved).
- [x] `MyRatings.razor` (task-006) lists rated/reviewed movies as "Movie #{tmdbId}" — no titles. — ✅ RESOLVED (Wave 5 task-002: resolves titles via a parallel `GET /movies/{id}` lookup, falls back to "Movie #{id}").
- [x] `SuggestRounds.razor` (task-004) requires a Pick to advance each round — there's no "skip this round" to move on without adding to the selection. The early "Finish & recommend now" covers bailing out, but a per-round skip (advance without picking) would be a natural UX addition. — ✅ RESOLVED (Wave 5 task-001: "Skip this round" button advances via `AdvanceAsync` without recording a pick; finishes on the last round).

### Wave 5 follow-ups (from reviewer)
- [ ] The reusable `ApiCall.RunAsync`/`ApiErrorBoundary` web error-handling helpers (task-001) are wired only into the two suggest pages. The other pages that call the API directly (`Search`, `Shuffle`, `MovieDetail`, `Collections`, `CollectionDetail`, `MyRatings`) still throw an unhandled Blazor exception on a network/API failure. Adopt the same helper across the remaining pages for consistent graceful failure.
- [ ] `AuthHardeningTests` (task-003) mirrors `PasswordHasher`'s iteration count as a private `CurrentIterations = 100_000` const. If the real `Iterations` is ever bumped, the `NeedsRehash_false_for_current_parameter_hash` test still passes (it hashes via `Hash()`), but the `MakeHash(.., CurrentIterations / 2)` "weak" fixtures silently stop reflecting the true gap. Consider exposing the cost (e.g. an internal const surfaced to tests) rather than duplicating the literal.
- [ ] The `/auth` rate-limiter (task-003) has no integration test asserting a 429 after the limit — the unit tests only cover `NeedsRehash`. The test file's comment claims the limiter is exercised end-to-end in `AuthFlowTests`; confirm that's actually true (a WebApplicationFactory test hammering `/auth/login` past 10/min) or add one, since rate-limit partitioning/IP-keying is easy to misconfigure silently.

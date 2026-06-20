# Changelog

Wave-by-wave record of what shipped and what we learned. Newest first.

## Wave 5 — 2026-06-19 (polish / hardening) — reviewer follow-ups cleared

Cleanup wave (no new features) — cleared the backlog of reviewer follow-ups. 6 tasks, all merged; build clean (0 warnings), format clean, **184 tests** (up from 173); API smoke ok (`/health`, `/collections`→401). Repo is public on GitHub, so this release **pushed to `origin/main`**.

**Shipped:**
- **Web UX** (`MovieNightPicker.Web`): reusable `ApiCall.RunAsync`/`ApiResult<T>` + `ApiErrorBoundary` banner (wired into the suggest pages); per-round **Skip** in the 10-round flow; **real movie titles** in My Ratings (parallel lookup); **parallel** member fetch in Collection detail (was N+1). (task-001, task-002)
- **API hardening** (`MovieNightPicker.Api`): fixed-window **rate-limit** on `/auth` (~10/min per IP, 429 over); **password rehash-on-login** (`PasswordHasher.NeedsRehash` + transparent re-hash/persist); reusable **`ValidationEndpointFilter<T>`** + `.WithRequestValidation<T>()` making the DataAnnotations real, manual rating/review checks removed. (task-003, task-004)
- **Code quality**: single **`TmdbMovie.ToCore()`** mapper (in `Tmdb.Dtos`) used by both the adapter and `InsightsService`; **`CachingTmdbClient` captive-dependency fix** — singleton decorator now resolves the inner `TmdbClient` per fetch via a `Func<ITmdbClient>`, restoring `IHttpClientFactory` handler rotation. (task-005, task-006)

**Process learnings (issues.md):**
- Workers kept committing their per-worktree `.claude/swarm-role.md` (reviewer stripped it from every merge). **Fixed at source:** added `.claude/swarm-role.md` to `.gitignore` this release.
- Moving validation out of a handler broke 2 tests that called the handler directly (filters don't run on bare handler calls); the test file wasn't in the task's `files` array. Lesson: when relocating validation, include the asserting test file in `files`.

**Still open (todos.md → Wave 5 follow-ups):** the web error-handling helper is only wired into the suggest pages (adopt across Search/Shuffle/Detail/Collections/MyRatings); `AuthHardeningTests` duplicates the iteration-count literal; no end-to-end 429 rate-limit test. Plus the deferred JWT refresh/revocation. All non-blocking.

## Wave 4 — 2026-06-18 (Blazor WASM frontend) — full-stack feature-complete

Added a **Blazor WebAssembly standalone** frontend (`MovieNightPicker.Web`) — the "full-stack .NET" stretch. Like Phase 0, a **foundation was scaffolded solo first** (the project, auth plumbing, shared display vocabulary), committed, then a 6-task page swarm built on top. All 6 merged first pass; build clean (0 warnings), format clean, 173 tests; smoke: API `/health` ok and the Blazor host page serves at :5032.

**Foundation (solo, `69b9c2a` + `a1f62d2`):** WASM project + slnx; auth plumbing — `TokenStore` (localStorage via IJSRuntime), `JwtAuthenticationStateProvider` (decodes the JWT payload by hand), `BearerTokenHandler`, `AuthClient` + Login/Register; default `HttpClient`→API (config `Api:BaseUrl`) with bearer; `CascadingAuthenticationState`/`AuthorizeRouteView`; **CORS policy on the API** (`WebClientCorsPolicy`); shared `MovieSummary` + `MovieCard` + all NavLinks pre-added.

**Swarm (page features):** browse (`MovieApiClient`, Search/Shuffle/Detail), suggest (`SuggestApiClient`, quick-suggest + 10-round flow), library (collections + insights, ratings/reviews + `RatingStars`). (task-001…006)

**Bottleneck fixes for an all-one-project wave:** the foundation pre-wired every shared file (`Program.cs`, `NavMenu`, `_Imports`) and feature API clients are plain classes pages **new up from the injected `HttpClient`** (no DI registration) — so zero feature task touched a shared file. Reviewer credited "name the exact API contracts to mirror + lock down shared files" for the clean first-pass merge.

**Environment notes:** `Blazored.LocalStorage` is NOT in this NuGet feed (used IJSRuntime); no bUnit either, so Web tasks validated via `dotnet build` (0 warnings) + the release runtime smoke, not unit tests.

**New follow-ups (todos.md → Wave 4):** web API calls lack error handling (unhandled exceptions vs friendly banner); `CollectionDetail` does N+1 movie fetches; `MyRatings` shows "Movie #{tmdbId}" (no titles); `SuggestRounds` has no per-round skip. Also note (Wave 3 carry-over): the `[Range]` rating attribute is decorative under minimal APIs — manual check is what enforces it.

## Wave 3 — 2026-06-18 (auth + user data + suggest/insights HTTP) — backend feature-complete

The final backend slice. 3 workers + reviewer, 6 tasks, all merged **first pass — zero rework**. Build clean (0 warnings), format clean, **173 tests** (up from 135). Smoke: `/health` ok; `/collections` returns 401 without a token (auth wired end-to-end).

**Shipped (all `MovieNightPicker.Api`):**
- **JWT auth:** `Auth/` (JwtTokenService, PasswordHasher, JwtOptions, `CurrentUser.GetUserId`), `AuthEndpoints` (register/login). (task-001)
- **User-scoped endpoints:** collections CRUD + add/remove movies (`CollectionService`), ratings + reviews upsert/CRUD with 1–10 request validation (`RatingReviewService`) — all ownership-scoped. (task-003, task-004)
- **Suggest/insights HTTP:** `POST /suggest/round/{n}` (drives Core `SuggestFlow`) + parallelized per-pick enrichment in `/movies/suggest`; `GET /collections/{id}/insights` (`InsightsService` builds `InsightsMovie`s from TMDB → `CollectionInsights.Compute`). (task-005, task-006)
- **Integration:** single task wired JWT middleware + mapped every endpoint module + registered services in `Program.cs`/`AddAppServices`/`appsettings`. (task-002)

**Resolved Wave-2 follow-ups:** suggest-round HTTP surface; insights HTTP surface; parallelized suggest enrichment.

**The Program.cs bottleneck, solved:** exactly one integration task (task-002, dependsOn all 5 others) edited `Program.cs`/`AddAppServices`/`appsettings`; one task (task-001) owned `Api.csproj`/`Tests.csproj`; feature tasks were file-disjoint and unit-tested via SQLite in-memory EF (no WebApplicationFactory until integration). Result: zero cross-worker conflicts on an all-in-one-project wave. The reviewer credited this file-ownership discipline + dependency-ordered task descriptions (reusable seams like `CurrentUser.GetUserId`) for the zero-rework run.

**New follow-ups (in todos.md → Wave 3 follow-ups):** no login rate-limiting; no JWT refresh/logout/revocation; no rehash-on-login if iteration count is raised; TMDB→Core `Movie` mapping now duplicated between `TmdbMovieDataSource` and `InsightsService` (extract a shared `TmdbMovie.ToCore()`).

**Process issue (swarm infra):** a worker briefly edited the main-repo working tree instead of its worktree (then recovered). Worker prompt clarified to state source edits go only in the worktree.

## Wave 2 — 2026-06-18 (API surface + Core leftovers + follow-ups)

The integrator wave. 3 workers + reviewer, 6 tasks, all merged. Build clean (0 warnings), format clean, **135 tests** (up from 65). `/health` smoke test passes with empty config.

**Shipped:**
- **API surface** (`MovieNightPicker.Api`): `TmdbMovieDataSource` adapter (`IMovieDataSource` over `ITmdbClient`), `AddAppServices` DI wiring, `GlobalExceptionHandler`→ProblemDetails (TmdbApiException→502), and read + suggest endpoints (`/movies/search`, `/movies/discover` shuffle, `/movies/{id}`, `/people/*`, `POST /movies/suggest`). `POST /movies/suggest` enriches picks via TMDB credits/keywords → full `PreferenceExtractor` profile. (task-003, task-004)
- **Core leftovers** (`MovieNightPicker.Core`): 10-round suggest flow (`SuggestFlow` + `SuggestRoundGenerator`, per-slot fetch/fallback, round-10 anchor inference) and `CollectionInsights.Compute`. (task-001, task-002)
- **Follow-ups** (`Tmdb`/`Data`): `CachingTmdbClient` decorator (TTL cache + in-flight dedup + 429 Retry-After backoff), query-key precedence fix (discover wins), and the `Rating` 1–10 check constraint + migration. (task-005, task-006)

**Resolved Wave-1 follow-ups:** DiscoverParams.GetHashCode excludes; query-key dedup; Rating check constraint; enriched-preferences wiring in the suggest endpoint.

**Design note:** Again three conflict-free directories (Core / Api / Tmdb+Data) despite this being the integrator wave — possible because the Wave-1 `IMovieDataSource` interface was stable, so the adapter implemented it without interface churn. JWT auth + user-scoped endpoints deferred to Wave 3.

**New follow-ups (in todos.md → Wave 2 follow-ups):** expose `SuggestFlow` and `CollectionInsights` via HTTP (Wave 3); a captive-dependency note on the singleton caching decorator (harmless for single-user); consider parallelizing per-pick enrichment in the suggest endpoint.

## Wave 1 — 2026-06-17 (Sets 1, 2, 4)

First swarm wave on top of the Phase 0 scaffold. 3 workers + reviewer, 7 tasks, all merged. Build clean (0 warnings), format clean, **65 tests** passing.

**Shipped:**
- **Set 1 — TMDB typed client** (`MovieNightPicker.Tmdb`): DTO records with `System.Text.Json` snake_case mapping, `TmdbQueryStringBuilder`, `ITmdbClient`/`TmdbClient` typed `HttpClient`, `TmdbApiException`, `AddTmdbClient` DI. (task-001, task-002)
- **Set 2 — Data layer** (`MovieNightPicker.Data`): all 8 EF Core entities + `MovieNightPickerDbContext` (Fluent config: unique indexes, cascades), `AddData` Npgsql wiring, design-time factory, `InitialCreate` migration. (task-003, task-004)
- **Set 4 — Core engine** (`MovieNightPicker.Core`): domain models + constant maps + `IMovieDataSource`, shuffle `DiscoverParamsBuilder` + progressive `FallbackChain`, `PreferenceExtractor` + 5-strategy `RecommendationCascade` — all LINQ-first. (task-005, task-006, task-007)

**Key design choice:** Core owns its own `IMovieDataSource` abstraction so Sets 1/2/4 had zero file overlap. The TMDB→Core adapter is intentionally deferred to the API wave (Set 3).

**Deferred to Wave 2:** 10-round suggest flow, collection insights aggregation, TMDB rate-limit/caching.

**Process learnings (from reviewer's issues log):**
- **EF Core version pinning:** Adding `Microsoft.EntityFrameworkCore.Design` (10.0.9) alongside the Npgsql provider (10.0.2) triggered MSB3277 (provider pins Relational to a different patch). Fix: pin `Microsoft.EntityFrameworkCore.Relational` to 10.0.9 explicitly to unify the stack. Future EF tasks should call out that base + Relational + Design must share one version satisfying the provider's range.
- **Stacked-task rebase fragility:** worker-3's task-007 branch still carried its task-006 commit (no rebase after task-006 squash-merged). `git merge --squash` netted correctly (identical content in main), but the cherry-pick path in `merge-worker.sh` could have hit an add/add conflict. Future: ensure worker auto-rebase runs after each squash-merge, or prefer `git merge --squash` over cherry-pick when a branch is behind.
- **Positive:** All 7 tasks stayed within their declared `files` array (zero scope creep). Detailed, self-contained task descriptions + a correct dependency graph (DTOs→client, entities→wiring, models→engine) were the main reasons it worked.

**Open follow-ups:** see `todos.md` → "Follow-ups (from reviewer)".

**Infra fix:** `scripts/taskswarm/spawn.sh` — made the `git update-index --assume-unchanged .claude/settings.json` non-fatal (that file is untracked here), with an `info/exclude` fallback.

# Changelog

Wave-by-wave record of what shipped and what we learned. Newest first.

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

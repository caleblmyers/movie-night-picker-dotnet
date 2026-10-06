# Movie Night Picker — .NET

A C#/.NET learning project implementing movie discovery, recommendations, collections, ratings, and reviews. It now includes an ASP.NET Core API **and a Blazor WebAssembly frontend**, built around TMDB and PostgreSQL.

The original TypeScript application lives in [the archived `movie-night-picker` repository](../movie-night-picker/README.md). This is an independent rewrite for learning idiomatic C#, LINQ, ASP.NET Core, and EF Core.

## Project checkpoint

Documentation reviewed 2026-10-05 against local source and manifests; application checks were not rerun for this review. Archive decision: archived at `archive/movie-night-picker-dotnet/` (owner decision, 2026-10-05).

**Current state:** Implemented ASP.NET Core API and Blazor WebAssembly frontend, with EF Core migrations and xUnit tests. The work queue records completed Waves 0–5 and remaining follow-ups.

**Stack:** .NET 10, ASP.NET Core, Blazor WebAssembly, EF Core/Npgsql, PostgreSQL, xUnit.

**Resume here:** Read `.claude-knowledge/todos.md` and `.claude-knowledge/app-overview.md`; continue the open follow-ups rather than rebuilding Phase 0.

**Agent guidance:** [AGENTS.md](AGENTS.md) contains the Codex/project instructions; [CLAUDE.md](CLAUDE.md) retains Claude-specific workflow context.

## Current implementation

- Movie/person search and detail, filtered discovery, suggestion flow, and recommendation logic
- Authentication and user-scoped collections, ratings, reviews, and insights
- Typed TMDB client with caching and fixture-based tests
- Blazor pages for discovery, suggestions, collections, and account flows
- EF Core migrations, API integration tests, and domain tests

The recorded work queue marks Waves 0–5 complete and retains small follow-ups and future deployment/auth work. Its historical test results have not been rerun for this documentation update.

## Requirements and configuration

- .NET 10 SDK (see `Directory.Build.props`)
- PostgreSQL for application data
- TMDB API key for live movie data

Supply API configuration through environment variables or local server-only configuration:

| Environment variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Tmdb__ApiKey` | TMDB credential |
| `Jwt__SigningKey` | JWT signing key; use a private value outside the development fallback |
| `Cors__AllowedOrigins__0` | Optional browser origin override |

The Blazor app reads `Api:BaseUrl` from `src/MovieNightPicker.Web/wwwroot/appsettings.json`; that file is public. Keep server secrets out of the web project.

## Run

```bash
dotnet restore
dotnet build
```

Apply existing migrations to the intended local development database when setting up a new instance (requires the EF CLI). Export `ConnectionStrings__Default` for this command: the design-time factory reads that environment variable directly rather than the API’s local configuration.

```bash
dotnet ef database update --project src/MovieNightPicker.Data --startup-project src/MovieNightPicker.Api
```

Start these in separate terminals with the server configuration available:

```bash
dotnet run --project src/MovieNightPicker.Api --launch-profile http
dotnet run --project src/MovieNightPicker.Web --launch-profile http
```

The checked-in HTTP launch profiles use API port **5196** and web port **5032**. The API exposes `/health`. Align the Blazor API URL and API CORS origins if you change ports.

## Validation

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Tests should use local fixtures/mocked TMDB access, not paid or live upstream requests.

## Repository map

- `MovieNightPicker.slnx` — solution (XML format, not `.sln`)
- `src/MovieNightPicker.Core/` — domain and recommendation logic
- `src/MovieNightPicker.Tmdb/` — external movie data client
- `src/MovieNightPicker.Data/` — EF Core entities and migrations
- `src/MovieNightPicker.Api/` — endpoints, auth, and application services
- `src/MovieNightPicker.Web/` — Blazor frontend
- `tests/MovieNightPicker.Tests/` — xUnit suite

## Returning to the project

- [Work queue](.claude-knowledge/todos.md) — completed waves and remaining work
- [Application overview](.claude-knowledge/app-overview.md) — architecture and important files
- [C# for TypeScript developers](docs/csharp-for-typescript-devs.md) — learning notes

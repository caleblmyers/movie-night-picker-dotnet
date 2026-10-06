# Movie Night Picker .NET — Dev Guide

C#/.NET rewrite of the Movie Night Picker backend as an ASP.NET Core Web API. Primary purpose: a **learning project** for getting fluent in C#/.NET. Favor idiomatic, modern C# over cleverness — readability and learning value first.

## Status

The API, Blazor WebAssembly frontend, EF Core migrations, and xUnit tests are implemented. `.claude-knowledge/todos.md` records Waves 0–5 complete plus remaining follow-ups. See `README.md` and `AGENTS.md` for current setup and commands; do not restart Phase 0.

## Stack

- **.NET 10** / C#, **ASP.NET Core** Web API and **Blazor WebAssembly** frontend
- **Entity Framework Core** + Npgsql (PostgreSQL)
- **xUnit** for tests
- External data: **TMDB** REST API (typed `HttpClient`)

## Commands

```bash
dotnet restore                 # install dependencies
dotnet build                   # compile (primary validation gate)
dotnet format --verify-no-changes   # style/format check
dotnet test                    # run the existing xUnit suite
dotnet run --project src/MovieNightPicker.Api --launch-profile http
dotnet run --project src/MovieNightPicker.Web --launch-profile http
dotnet ef migrations add <Name> --project src/MovieNightPicker.Data --startup-project src/MovieNightPicker.Api
dotnet ef database update --project src/MovieNightPicker.Data --startup-project src/MovieNightPicker.Api
```

## Current structure

```
MovieNightPicker.slnx
src/
  MovieNightPicker.Api/        # ASP.NET Core Web API (controllers/endpoints, DI, config)
  MovieNightPicker.Web/        # Blazor WebAssembly frontend
  MovieNightPicker.Core/       # domain models + business logic (suggestion cascade, filters)
  MovieNightPicker.Data/       # EF Core DbContext, entities, migrations
  MovieNightPicker.Tmdb/       # typed HttpClient wrapper over the TMDB REST API
tests/
  MovieNightPicker.Tests/      # xUnit
```

The solution contains five application/library projects and one test project.

## Conventions

- Idiomatic modern C#: nullable reference types on, `record`/pattern matching where they fit, `async`/`await` end to end.
- LINQ for all querying/filtering — this is the skill the project is meant to build.
- Don't hit the real TMDB API in tests — mock the typed client.
- Secrets via user-secrets / env, never committed. `appsettings.Development.json` is gitignored.

## What's being ported (from the TS original)

See `.claude-knowledge/app-overview.md`. Core logic to preserve: the 10-round suggestion flow, the 5-strategy recommendation cascade, the 15+ shuffle filters, collections/ratings persistence. Multi-tenant/SaaS concerns from the original's siblings do **not** apply — this is single-user.

## AI dev infrastructure

- `.claude-knowledge/` — read before starting work; update as decisions/errors accrue.
- Swarm: `/task-swarm` to plan + spawn, `/task-worker`, `/task-reviewer`, `/task-release`. Swarm is for Phase 1+ once code exists; Phase 0 scaffolding is solo.

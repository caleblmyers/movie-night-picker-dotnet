# Movie Night Picker .NET — Dev Guide

C#/.NET rewrite of the Movie Night Picker backend as an ASP.NET Core Web API. Primary purpose: a **learning project** for getting fluent in C#/.NET. Favor idiomatic, modern C# over cleverness — readability and learning value first.

## Status

**Phase 0 complete — solution scaffolded, build/test/format green.** 5 projects under `src/` + `tests/`, wired into `MovieNightPicker.slnx`. Next: Phase 1+ via the swarm (see `.claude-knowledge/todos.md`). The build/test commands below are live (solution file is `.slnx`, not `.sln`).

## Target stack

- **.NET 10** / C#, **ASP.NET Core** Web API
- **Entity Framework Core** + Npgsql (PostgreSQL)
- **xUnit** for tests
- External data: **TMDB** REST API (typed `HttpClient`)

## Commands

```bash
dotnet restore                 # install dependencies
dotnet build                   # compile (primary validation gate)
dotnet format --verify-no-changes   # style/format check
dotnet test                    # run tests (once a test project exists)
dotnet run --project <api>     # run the API
dotnet ef migrations add <Name>     # EF Core: new migration after model changes
dotnet ef database update           # apply migrations
```

## Intended structure (once scaffolded)

```
MovieNightPicker.sln
src/
  MovieNightPicker.Api/        # ASP.NET Core Web API (controllers/endpoints, DI, config)
  MovieNightPicker.Core/       # domain models + business logic (suggestion cascade, filters)
  MovieNightPicker.Data/       # EF Core DbContext, entities, migrations
  MovieNightPicker.Tmdb/       # typed HttpClient wrapper over the TMDB REST API
tests/
  MovieNightPicker.Tests/      # xUnit
```

(Layout is a starting proposal — revisit at scaffold time.)

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

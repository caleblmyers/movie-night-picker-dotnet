# Movie Night Picker — .NET API

A C#/.NET rewrite of the [Movie Night Picker](../movie-night-picker) backend, rebuilt as an **ASP.NET Core Web API**. This is a **learning project** — the vehicle for getting fluent in C#/.NET ahead of the JMS Digital Solutions Engineer role (Epicor customization + on-prem .NET).

> **Status: repo shell only.** No application code yet — no solution/projects. The AI-dev infrastructure (`.claude-knowledge/`, swarm) is in place; the .NET solution gets scaffolded next (Phase 0). See `.claude-knowledge/todos.md`.

## Why this rewrite

The original is a TypeScript Express/Apollo GraphQL API wrapping the TMDB REST API + PostgreSQL. Its shape maps almost 1:1 onto the target job, which makes it an ideal teacher:

| Original (TS) | This rewrite (C#/.NET) | Why it transfers to the job |
|---|---|---|
| Express / Apollo | **ASP.NET Core Web API** | On-prem internal .NET tools |
| Wraps TMDB REST API | `HttpClient` + typed clients | Wrapping **Epicor REST / BAQ** endpoints |
| 15+ filters, suggestion cascade | **LINQ** | Dashboard / BAQ-style data querying |
| Prisma + PostgreSQL | **Entity Framework Core** | On-prem SQL data access |
| JWT auth | ASP.NET Core JWT bearer auth | — |

Scope is the **API/backend** (not the React frontend) — that's where the transferable C# lives. Optional later stretch: a Blazor UI slice for full-stack .NET.

## Target stack

- **.NET 10** / C#
- **ASP.NET Core** Web API
- **Entity Framework Core** + Npgsql (PostgreSQL)
- **xUnit** for tests
- **TMDB** as the external data source (need a TMDB API key)

## Getting started (once the solution is scaffolded)

```bash
dotnet restore
dotnet build
dotnet run --project src/MovieNightPicker.Api   # path TBD at scaffold time
```

## AI-assisted development

This repo carries the brain's reusable AI infrastructure:

- **`.claude-knowledge/`** — persistent knowledge base (architecture, decisions, todos) consulted across Claude Code sessions.
- **`.ai/taskswarm/` + `scripts/taskswarm/`** — multi-agent parallel-dev swarm (planner / workers / reviewer over git worktrees).
- **`.claude/skills/`** — `/task-swarm`, `/task-worker`, `/task-reviewer`, `/task-release`.

The swarm is for **Phase 1+** (parallelizable work in an existing codebase). Phase 0 (scaffolding the solution) is a **solo** agent — nothing to parallelize yet.

## Planning docs (in the brain repo)

- `~/brain/knowledge/csharp/study-plan.md` — the overall C# learning plan this project anchors
- `~/brain/projects/movie-night-picker/overview.md` — the original app's features + architecture

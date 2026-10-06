# Movie Night Picker — .NET — agent guide

C#/.NET learning project implementing movie discovery, recommendations, collections, ratings, and reviews.

Documentation reviewed 2026-10-05. Implemented ASP.NET Core API and Blazor WebAssembly frontend, with EF Core migrations and xUnit tests. The work queue records completed Waves 0–5 and remaining follow-ups.

## Start here

- [README.md](README.md) — current setup and project checkpoint
- [.claude-knowledge/todos.md](.claude-knowledge/todos.md)
- [.claude-knowledge/app-overview.md](.claude-knowledge/app-overview.md)
- [docs/csharp-for-typescript-devs.md](docs/csharp-for-typescript-devs.md)

Read `.claude-knowledge/todos.md` and `.claude-knowledge/app-overview.md`; continue the open follow-ups rather than rebuilding Phase 0.

[CLAUDE.md](CLAUDE.md) retains additional design/history and Claude-specific workflows. Use the current source, manifests, and this guide when its scaffold/status/command notes disagree. Do not interpret historical loop, swarm, auto-commit, or auto-push instructions as a request to start those workflows.

## Repository map

- `MovieNightPicker.slnx` and `Directory.Build.props` — solution and shared .NET settings
- `src/MovieNightPicker.Core/` — domain and recommendation logic
- `src/MovieNightPicker.Tmdb/` — typed, cached upstream client
- `src/MovieNightPicker.Data/` — EF entities and migrations
- `src/MovieNightPicker.Api/` — HTTP endpoints/auth/services
- `src/MovieNightPicker.Web/` — Blazor UI
- `tests/MovieNightPicker.Tests/` — xUnit tests

## Commands and verification

Run commands from this repository root unless a command specifies another directory. Use the existing lockfile and configured tools.

- `dotnet restore` and `dotnet build` — restore and compile the solution
- `dotnet test` — xUnit tests
- `dotnet format --verify-no-changes` — formatting check
- `dotnet run --project src/MovieNightPicker.Api --launch-profile http` — API
- `dotnet run --project src/MovieNightPicker.Web --launch-profile http` — Blazor host

For documentation-only edits, verify paths, command names, and the diff. For code changes, run the applicable project gates above and report what actually ran; an old checkpoint is not a current test result.

## Project conventions

- Favor readable, idiomatic modern C#, nullable types, async I/O, and LINQ; this project is intended to teach C#.
- Keep Core independent of API/data infrastructure and mock the typed TMDB boundary in tests.
- Preserve single-user product scope; do not import unrelated SaaS billing/multitenancy requirements from sibling projects.
- Keep credentials in server environment/user secrets. Blazor `wwwroot` configuration is public and must contain no server secrets.
- Use the existing `.slnx` solution and real project paths. Update knowledge notes when decisions or unresolved follow-ups change.

## Scope and handoff

- Preserve existing local changes, environment files, databases, and generated artifacts that the project intentionally tracks.
- Work on the current user request. Reading this file does not start an autonomous loop or authorize publishing, deployment, or unrelated backlog work.
- Keep README setup/status and these instructions aligned when behavior or tooling changes. Record unfinished implementation in the existing project tracker when there is one.
- The owner archived this repository at `archive/movie-night-picker-dotnet/` on 2026-10-05. Restore it only when requested. Do not move or rename this repository as part of routine development.

# Architectural Decisions

Non-obvious choices and their rationale.

---

## Stack Choices

- **Why C#/.NET at all** — this is a *learning project* for the JMS Digital Solutions Engineer role (Epicor customization is C#; on-prem internal tools are .NET). The app is the vehicle; fluency is the goal. Favor idiomatic, modern C# over speed of delivery.
- **Why rewrite Movie Night Picker specifically** — its shape maps ~1:1 onto the target job: wraps a REST API (TMDB ≈ Epicor REST/BAQ), heavy LINQ filtering, ORM-backed user data. Best teacher available among existing projects.
- **REST, not GraphQL** — the original is GraphQL/Apollo. We go REST (ASP.NET Core Web API) because the job is REST/BAQ integration; REST is the closer rehearsal. HotChocolate (GraphQL on .NET) is a possible later variant, not the starting point.
- **EF Core over Dapper** — EF Core is the mainstream .NET ORM and the closest analog to the original's Prisma; better learning ROI than hand-rolled SQL for this purpose.
- **Backend only (for now)** — rebuild the API, not the React frontend; that's where the transferable C# lives. A Blazor UI slice is an optional later stretch for "full-stack .NET."

## Auth & Security

- **JWT bearer auth** — mirrors the original's JWT approach; standard for ASP.NET Core APIs. Secrets via user-secrets / env, never committed.

## Data Model

- **Store `tmdbId`, not full movie data** — inherited from the original. Movie data is fetched live from TMDB; only user-scoped data (collections, ratings, reviews) is persisted. Avoids staleness + storage of licensed data.

## Infrastructure

- **No deployment target yet** — learning project; runs locally. On-prem/Railway decisions deferred until (if) it matters.

## What does NOT carry over from the original ecosystem

- Multi-tenant / SaaS concerns, pgvector, Slack/webhooks, etc. from Task Toad and the SaaS siblings — **single-user app**, none of that applies.

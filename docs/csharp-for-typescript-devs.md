# C# / .NET for TypeScript Developers

A concept-by-concept map from the **original TypeScript app** (Next.js + Express/Apollo/Prisma) to this **C#/.NET 10 rewrite** (Blazor + ASP.NET Core/EF Core). Both implement the same Movie Night Picker, so every comparison below is the *same feature* expressed in each stack.

- **Original (TS):** `~/projects/archive/movie-night-picker` — `apps/web` (Next.js 16 / React 19 / Apollo Client), `apps/api` (Express 5 / Apollo Server 4 / Prisma 6), `packages/shared-types`.
- **This repo (C#):** `MovieNightPicker.Web` (Blazor WASM), `.Api` (ASP.NET Core), `.Core`, `.Data` (EF Core), `.Tmdb`, `tests/` (xUnit).

> Biggest mental-model shifts up front: (1) **types are nominal and compiled**, not erased structural shapes; (2) **dependency injection is a first-class container with lifetimes**, not a hand-built `context` object; (3) **LINQ replaces array methods** and is the idiom to lean into; (4) the API is **REST, not GraphQL**.

---

## 0. The 30-second map

| TypeScript / Node / React | C# / .NET | Notes |
|---|---|---|
| `interface`, `type` (structural) | `interface`, `record`, `class` (nominal) | Names matter; duck typing is gone |
| `type X = A \| B` union | `enum` + `switch` expression / record hierarchy | No anonymous unions |
| `foo?: T` / `T \| null` | `T?` (nullable reference types) | Compiler-enforced like `strictNullChecks` |
| `Promise<T>` / `async`/`await` | `Task<T>` / `async`/`await` | Nearly identical |
| `arr.map/filter/find/reduce` | `arr.Select/Where/First/Aggregate` (LINQ) | Same ideas, different names |
| `Promise.all([...])` | `Task.WhenAll(...)` | |
| `throw new Error(msg)` | `throw new SomeException(msg)` | Typed exceptions, `catch (T ex) when (...)` |
| npm/pnpm + `package.json` | NuGet + `.csproj` `<PackageReference>` | |
| `pnpm install` / `pnpm -r build` | `dotnet restore` / `dotnet build sln` | |
| `tsconfig.json` `strict` | `Directory.Build.props` (`Nullable`, analyzers) | |
| Prisma `schema.prisma` + client | EF Core entities + `DbContext` + LINQ | |
| Apollo `context` object | DI container (`AddScoped/Singleton`) | |
| GraphQL resolver | Minimal-API endpoint handler | |
| `bcryptjs` / `jsonwebtoken` | PBKDF2 (`Rfc2898DeriveBytes`) / `JwtBearer` | |
| React component + hooks | Blazor `.razor` component + lifecycle | |
| `useState` | a private field (+ auto re-render) | |
| `useEffect(…, [])` | `OnInitializedAsync()` | |
| Apollo `useQuery` | inject `HttpClient` + `GetFromJsonAsync` | |
| Next.js `app/movie/[id]/page.tsx` | `@page "/movies/{Id:int}"` | |
| NextAuth + `localStorage` | `AuthenticationStateProvider` + token store | |
| no tests (!) | xUnit, 184 tests | |

---

## 1. Project structure & build

**TS** — a pnpm workspace; each app/package has its own `package.json` and `tsconfig.json`:

```yaml
# pnpm-workspace.yaml
packages: ["apps/*", "packages/*"]
```
```jsonc
// tsconfig.base.json (extended by apps)
{ "compilerOptions": { "strict": true, "esModuleInterop": true, "skipLibCheck": true } }
```

**C#** — a *solution* (`.slnx`) groups *projects* (`.csproj`). Shared compiler settings live in one `Directory.Build.props` at the root (the analog of `tsconfig.base.json`); each `.csproj` only carries what's unique (its package/project refs):

```xml
<!-- Directory.Build.props — applies to every project -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>              <!-- ≈ strictNullChecks -->
    <ImplicitUsings>enable</ImplicitUsings>  <!-- ≈ auto-imported globals -->
    <LangVersion>latest</LangVersion>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

| Task | TS | C# |
|---|---|---|
| install deps | `pnpm install` | `dotnet restore` |
| build everything | `pnpm -r run build` | `dotnet build MovieNightPicker.slnx` |
| run one app | `pnpm --filter api dev` | `dotnet run --project src/MovieNightPicker.Api` |
| add a dependency | edit `package.json` / `pnpm add` | `dotnet add <proj> package <name>` |
| reference another project | workspace dep | `<ProjectReference Include="..." />` |

A key difference: **C# projects compile to separate assemblies (DLLs) with enforced dependency direction.** This repo's graph is `Core ← Data, Tmdb`; `Api → Core/Data/Tmdb`. You physically *cannot* create a circular reference — the compiler refuses. In the TS monorepo, import direction is only enforced by convention/lint.

---

## 2. The language

### 2.1 Interfaces, types, and records

TS data shapes are `interface`/`type` (structural — anything with the right fields matches). C# uses `record` for immutable data, `class` for entities/services, `interface` for contracts (all **nominal** — the declared type name is what matches).

```ts
// TS — resolver args / a DTO are just interfaces
export interface AuthArgs { email: string; password: string; }
export interface JWTPayload { userId: number; email: string; }
```
```csharp
// C# — a positional record: immutable, value-equality, concise
public sealed record RegisterRequest(string Email, string Password, string Name);
public sealed record AuthResponse(string Token, int UserId, string Email, string Name);
```

`record` gives you value equality, `with`-expressions (non-destructive copy), and a tidy constructor — think "a `type` that's also immutable and comparable." This repo uses records for every DTO/domain model (`Movie`, `DiscoverParams`, `MovieSummary`, …) and classes for EF entities + services.

### 2.2 Null handling

You already live in `strictNullChecks`. C#'s **nullable reference types** are the same deal: `string` is non-null, `string?` is nullable, and the compiler warns on unsafe dereferences.

```ts
poster_path: string | null;     // TS
options?: TMDBOptions;          // optional param
const m: Movie | null = ...;
```
```csharp
public string? PosterPath { get; init; }                       // nullable
public Task<TmdbMovie> GetMovieAsync(int id, TmdbRequestOptions? options = null, …);
CoreModels.Movie? movie = ...;
```

Handy operators that map almost 1:1: `?.` (both), `??` (both), `!` non-null assertion (both — `x!`), and C# adds `??=`. The codegen config in TS even spelled this out: `maybeValue: "T | null"`.

### 2.3 Unions → enums + pattern matching

TS leans on union types. C# has no anonymous unions; you reach for an `enum` plus a `switch` *expression* (an expression, so it returns a value), or a record hierarchy with pattern matching.

```ts
// TS — string union
type RoundCategory = "genre" | "era" | "mood" | "popularity" | "mixed";
```
```csharp
// C# — enum + switch expression (from the real exception handler)
var problem = exception switch
{
    TmdbApiException tmdb => new ProblemDetails { Status = 502, Detail = tmdb.Message },
    _                     => new ProblemDetails { Status = 500 },
};
```

Pattern matching is genuinely nicer than TS here. This idiom appears all over the API:

```csharp
// "if the user id claim is present, bind it to userId; else 401"
if (user.GetUserId() is not { } userId)
    return TypedResults.Unauthorized();
```

### 2.4 Generics

Same concept, angle brackets and all.

```ts
function useDebounce<T>(value: T, delay: number): T { ... }
async makeRequest<T>(endpoint: string): Promise<T> { ... }
```
```csharp
public sealed record TmdbPagedResult<T>(int Page, IReadOnlyList<T> Results, int TotalPages);
public async Task<T> GetAsync<T>(string url, CancellationToken ct) { ... }
```

### 2.5 async / await

Almost identical. `Promise<T>` → `Task<T>`, `Promise.all` → `Task.WhenAll`. C# adds `CancellationToken` (a cooperative cancel signal you thread through — there's no exact TS equivalent; closest is an `AbortSignal`).

```ts
const [movie, videos, credits] = await Promise.all([ getMovie(id), getVideos(id), getCredits(id) ]);
```
```csharp
// from the parallelized suggest enrichment
var enriched = await Task.WhenAll(ids.Select(id => EnrichAsync(id, ct)));
```

### 2.6 Collections & LINQ (the one to internalize)

LINQ is the C# version of array methods — this whole project was partly an excuse to get fluent in it. Map the names:

| TS array method | LINQ |
|---|---|
| `.map(f)` | `.Select(f)` |
| `.filter(p)` | `.Where(p)` |
| `.find(p)` | `.FirstOrDefault(p)` |
| `.some/.every` | `.Any/.All` |
| `.reduce` | `.Aggregate` |
| `.sort((a,b)=>…)` | `.OrderBy/.OrderByDescending/.ThenBy` |
| `.slice(0,n)` | `.Take(n)` |
| `[...new Set(x)]` | `.Distinct()` |
| `.flatMap` | `.SelectMany` |
| `.length` | `.Count()` / `.Count` |

```ts
// TS resolver mapping a TMDB page
return results.map(transformTMDBMovie);
```
```csharp
// C# adapter doing the same
return page.Results.Select(m => m.ToCore()).ToList();
```

A meatier real example — the preference extractor's "top-N features above a confidence threshold" reads like the array-method version you'd write in TS, just with LINQ:

```csharp
var ranked = ids
    .GroupBy(id => id)
    .Select(g => new { Id = g.Key, Count = g.Count() })
    .OrderByDescending(x => x.Count)
    .ThenBy(x => x.Id)
    .ToList();
```

> Gotcha: LINQ is **lazy** (deferred execution). `.Select(...)` doesn't run until you enumerate (`.ToList()`, `foreach`, `.Count()`). And the *same* LINQ over EF Core's `DbSet` is translated to SQL instead of running in memory — see §6.

### 2.7 Errors

TS throws plain `Error`; the original wraps everything in generic `Error`. C# uses **typed exceptions** and filtered catches:

```ts
export function handleError(error: unknown, msg: string): Error {
  if (error instanceof Error) return new Error(`${msg}: ${error.message}`);
  return new Error(`${msg}: Unknown error`);
}
```
```csharp
public sealed class TmdbApiException(int statusCode, string message) : Exception(message)
{ public int StatusCode { get; } = statusCode; }

// catch only 404s, let everything else propagate — note the `when` filter
catch (TmdbApiException ex) when (ex.StatusCode == 404) { return null; }
```

---

## 3. Backend framework: Express + Apollo (GraphQL) → ASP.NET Core (REST)

### 3.1 Bootstrapping

**TS** (`server.ts`): build an Apollo server, mount it on Express, pass a `context` factory.

```ts
const server = new ApolloServer({ typeDefs, resolvers, formatError: (e) => { console.error(e); return e; } });
await server.start();
app.use("/graphql", cors({ origin: process.env.FRONTEND_URL }), express.json(),
  expressMiddleware(server, { context: createContext }));
```

**C#** (`Program.cs`): a builder registers services, then a pipeline of middleware, then endpoints. Note how the GraphQL `context` factory's job (wiring dependencies) is instead done by the DI container, and `formatError` becomes a global exception handler.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAppServices(builder.Configuration, builder.Environment); // DI registrations
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors(ServiceCollectionExtensions.WebClientCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapMovieEndpoints();      // each feature module maps its routes
app.MapAuthEndpoints();
app.Run();
```

### 3.2 Resolver → endpoint

A GraphQL resolver `(parent, args, context)` becomes a minimal-API handler whose **parameters are injected** (route values, request body, and services all by type).

```ts
// TS resolver — pulls prisma + tmdb off the context object
getMovie: async (_p, args: GetMovieArgs, context: Context): Promise<Movie> => {
  const tmdbMovie = await context.tmdb.getMovie(args.id, ...);
  return transformTMDBMovie(tmdbMovie);
},
```
```csharp
// C# endpoint — `id` from the route, `client` injected by DI, returns typed results
private static async Task<IResult> GetDetailAsync(int id, ITmdbClient client, CancellationToken ct)
{
    try { return Results.Ok(MovieResponse.FromTmdb(await client.GetMovieAsync(id, ct: ct))); }
    catch (TmdbApiException ex) when (ex.StatusCode == 404) { return Results.NotFound(); }
}
```

Routes are grouped and given cross-cutting concerns fluently — this replaces GraphQL's single `/graphql` endpoint:

```csharp
var ratings = app.MapGroup("/ratings").RequireAuthorization();  // auth on the whole group
ratings.MapGet("/", ListAsync);
ratings.MapPut("/{tmdbId:int}", UpsertAsync).WithRequestValidation<UpsertRatingRequest>();
```

### 3.3 Schema/types: SDL strings → C# records

GraphQL is **schema-first** (SDL strings, types declared in `.ts` and codegen'd into TS types). REST in C# just serializes records to/from JSON via `System.Text.Json` — the C# type *is* the contract.

```ts
type Movie { id: Int!  title: String!  overview: String  voteAverage: Float }
```
```csharp
public sealed record MovieResponse(int Id, string Title, string? Overview, double? VoteAverage);
```

TMDB returns `snake_case`; the `[JsonPropertyName]` attribute does what a codegen mapping would:

```csharp
public sealed record TmdbMovie(
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("vote_average")] double? VoteAverage);
```

---

## 4. Dependency injection — the big one

This is the most important conceptual jump. The TS app builds a **`context` object by hand** on every request and threads it through resolvers. ASP.NET Core has a **DI container**: you register services with a *lifetime*, declare what you need as constructor/parameter types, and the framework constructs the graph for you.

```ts
// TS — manual "DI": context.ts builds the object per request
const prisma = new PrismaClient();                          // module singleton
export const createContext = async ({ req, res }) => {
  const tmdb = new TMDBDataSource(process.env.TMDB_API_KEY!); // new per request
  let user = null;
  const token = extractTokenFromHeader(req.headers.authorization);
  if (token) { try { user = await prisma.user.findUnique({ where: { id: verifyToken(token).userId } }); } catch {} }
  return { prisma, tmdb, req, res, user };
};
// ...and every resolver reads context.prisma / context.tmdb / context.user
```
```csharp
// C# — register once, with lifetimes; the container injects by type
public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config, ...)
{
    services.AddTmdbClient(o => o.ApiKey = config["Tmdb:ApiKey"] ?? "");  // typed HttpClient
    services.AddData(config.GetConnectionString("Default") ?? "");        // DbContext (scoped)
    services.AddScoped<IMovieDataSource, TmdbMovieDataSource>();          // interface -> impl
    services.AddScoped<CollectionService>();
    services.AddSingleton<PasswordHasher>();
    return services;
}
```

Lifetimes are the formalized version of "singleton vs per-request":

| Lifetime | Created | TS analog here |
|---|---|---|
| **Singleton** | once for the app | `const prisma = new PrismaClient()` at module load |
| **Scoped** | once per HTTP request | `new TMDBDataSource(...)` inside `createContext` |
| **Transient** | every time it's asked for | — |

And **current user**: TS resolves it manually in `createContext` and you call `requireAuth(context)`. C# validates the JWT in middleware automatically; you mark endpoints `[Authorize]`/`.RequireAuthorization()` and read the user from the injected `ClaimsPrincipal`.

```ts
export function requireAuth(context: Context): User {
  if (!context.user) throw new AuthenticationError("Authentication required");
  return context.user;
}
```
```csharp
// CurrentUser.cs — extension on the injected ClaimsPrincipal
public static int? GetUserId(this ClaimsPrincipal user) =>
    int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
```

---

## 5. ORM: Prisma → EF Core

### 5.1 Schema definition

Prisma has its own `.prisma` DSL. EF Core uses **C# classes + a `DbContext`**, with constraints/indexes configured either by attributes or the Fluent API in `OnModelCreating`.

```prisma
model SavedMovie {
  id        Int      @id @default(autoincrement())
  tmdbId    Int
  userId    Int
  user      User     @relation(fields: [userId], references: [id], onDelete: Cascade)
  @@unique([userId, tmdbId])
  @@index([userId])
}
```
```csharp
// Data/Entities/SavedMovie.cs
public class SavedMovie
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
// MovieNightPickerDbContext.OnModelCreating
modelBuilder.Entity<SavedMovie>(e =>
{
    e.HasIndex(s => new { s.UserId, s.TmdbId }).IsUnique();   // @@unique
    e.HasIndex(s => s.UserId);                                // @@index
    e.HasOne(s => s.User).WithMany(u => u.SavedMovies)
     .OnDelete(DeleteBehavior.Cascade);                       // onDelete: Cascade
});
```

| Prisma | EF Core |
|---|---|
| `@id @default(autoincrement())` | `int Id` (convention) |
| `@unique` / `@@unique([...])` | `HasIndex(...).IsUnique()` |
| `@@index([...])` | `HasIndex(...)` |
| `@relation(onDelete: Cascade)` | `.OnDelete(DeleteBehavior.Cascade)` |
| `@updatedAt` | set in code / interceptor |

### 5.2 Queries — Prisma client vs LINQ

```ts
const existing = await context.prisma.user.findUnique({ where: { email: args.email } });
const user = await context.prisma.user.create({ data: { email, password: hashed, name } });
```
```csharp
var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
db.Users.Add(new User { Email = email, Password = hashed, Name = name });
await db.SaveChangesAsync(ct);
```

The big idea: **the same LINQ you'd run in memory is translated to SQL** when the source is a `DbSet`. `db.Ratings.Where(r => r.UserId == id)` becomes a `WHERE` clause. (This is closer to Prisma's query builder than to writing SQL — you rarely touch SQL.)

### 5.3 Migrations

| | TS (Prisma) | C# (EF Core) |
|---|---|---|
| create a migration | `prisma migrate dev` | `dotnet ef migrations add <Name>` |
| apply to DB | `prisma migrate deploy` | `dotnet ef database update` |
| generate client | `prisma generate` (postinstall) | (not needed — it's just C#) |

---

## 6. Calling TMDB: axios + mixins → typed `HttpClient` + decorator

**TS**: an axios instance with the api key as a default param, a generic `makeRequest<T>`, and a base class composed with **mixins** for movie/people/credits methods.

```ts
this.client = axios.create({ baseURL: TMDB_BASE_URL, params: { api_key: this.apiKey } });
protected async makeRequest<T>(endpoint, params?): Promise<T> {
  try { return (await this.client.get(endpoint, { params })).data; }
  catch (e) { throw handleTMDBError(e, "Failed to fetch from TMDB"); }
}
```

**C#**: a *typed `HttpClient`* registered with the DI container; an interface (`ITmdbClient`) is the contract; and instead of mixins, caching is layered on with the **decorator pattern** (`CachingTmdbClient` wraps the real client — same idea as composing behavior, but explicit).

```csharp
// registration — the framework manages the HttpClient + its handler pool
services.AddHttpClient<TmdbClient>();
services.AddSingleton<ITmdbClient>(sp => new CachingTmdbClient(/* inner */ ..., cache));

// a method (System.Net.Http.Json gives GetFromJsonAsync — like axios returning .data typed)
public async Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(DiscoverParams p, ...)
{
    var url = $"/discover/movie?{TmdbQueryStringBuilder.Build(p, _options.ApiKey)}";
    return await _http.GetFromJsonAsync<TmdbPagedResult<TmdbMovie>>(url, ct) ?? Empty;
}
```

TS mixins ≈ C# `partial class` or interface composition; the cross-cutting cache here is a decorator registered as the interface, so callers depend on `ITmdbClient` and never know caching exists.

---

## 7. Auth

| Concern | TS | C# |
|---|---|---|
| hash password | `bcryptjs` (`bcrypt.hash`, salt rounds 10) | PBKDF2 via `Rfc2898DeriveBytes` (`PasswordHasher`) |
| sign/verify JWT | `jsonwebtoken` (`jwt.sign/verify`) | `JwtSecurityToken` + `AddJwtBearer` |
| who is the user? | manual `verifyToken` in `createContext` | `AddJwtBearer` validates automatically per request |
| protect a route | `requireAuth(context)` guard | `[Authorize]` / `.RequireAuthorization()` |
| client attaches token | Apollo `authLink` from `localStorage` | `BearerTokenHandler` from token store |

```ts
export function generateToken(p: JWTPayload) { return jwt.sign(p, JWT_SECRET, { expiresIn: "7d" }); }
const hashed = await bcrypt.hash(password, 10);
```
```csharp
var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
// JwtTokenService builds a signed JWT with NameIdentifier + email claims
```

Note both apps store the JWT in `localStorage` and attach it as a `Bearer` header on the client — that part is a near-exact 1:1 (see §10.5).

---

## 8. Validation

The TS app validates **manually** (no zod/class-validator): helper functions return `{ valid, error }` or `throw new Error(...)`. C# uses **DataAnnotations** attributes made real by a reusable endpoint filter.

```ts
export function validatePassword(p: string) {
  if (p.length < 6) return { valid: false, error: "Password must be at least 6 characters long" };
  return { valid: true };
}
```
```csharp
public sealed record UpsertRatingRequest([property: Range(1, 10)] int Value);

// one reusable filter runs the annotations and short-circuits with a 400 ProblemDetails
ratings.MapPut("/{tmdbId:int}", UpsertAsync).WithRequestValidation<UpsertRatingRequest>();
```

(Gotcha learned the hard way in this repo: minimal APIs **don't** auto-run DataAnnotations — the attribute is decorative until a filter executes it. That filter is `ValidationEndpointFilter<T>`.)

---

## 9. Config & secrets

```ts
dotenv.config();
const key = process.env.TMDB_API_KEY!;
const secret = process.env.JWT_SECRET || "your-secret-key-change-in-production"; // insecure fallback
```
```csharp
// appsettings.json + env vars + user-secrets all merge into IConfiguration
var key = config["Tmdb:ApiKey"] ?? "";
var conn = config.GetConnectionString("Default") ?? "";
```

| TS | C# |
|---|---|
| `.env` / `process.env.X` | `appsettings.json` → `IConfiguration["Section:Key"]` |
| `.env.local` (gitignored) | `appsettings.Development.json` (gitignored) / **user-secrets** |
| `NEXT_PUBLIC_*` (client-exposed) | `wwwroot/appsettings.json` in the WASM app (shipped to the browser) |
| env var `FOO_BAR` | env var `Foo__Bar` (`__` = section nesting) |

Config is **layered**: appsettings → environment-specific appsettings → user-secrets → env vars, last-wins. No `dotenv` import needed; it's built in.

---

## 10. Frontend: React/Next → Blazor WASM

Both are component SPAs that call the API with a bearer token. Blazor components are `.razor` files mixing markup and C#; the concepts map cleanly.

### 10.1 Component + props

```tsx
interface ShuffleMovieCardProps { movie: Movie; onShuffleAgain: () => void; }
function ShuffleMovieCard({ movie, onShuffleAgain }: ShuffleMovieCardProps) {
  return <Button onClick={onShuffleAgain}>Pick Another</Button>;
}
```
```razor
@* MovieCard.razor *@
<div class="card">@Movie.Title</div>
@code {
    [Parameter, EditorRequired] public required MovieSummary Movie { get; set; }
    [Parameter] public RenderFragment<MovieSummary>? Actions { get; set; }   // ≈ children/slot
}
```

Props → `[Parameter]` properties. Callbacks (`() => void`) → `EventCallback`. `children`/render-prop slots → `RenderFragment`.

### 10.2 State: `useState` → fields

```tsx
const [query, setQuery] = useState("");
// later: setQuery("dune")  → triggers re-render
```
```razor
@code {
    private string query = "";        // just a field
    // later: query = "dune";  → component re-renders automatically after an event handler
}
```

No `setState`: you mutate the field, and Blazor re-renders after the event handler returns (call `StateHasChanged()` only for out-of-band updates, e.g. a timer).

### 10.3 `useEffect` → lifecycle methods

```tsx
useEffect(() => { /* load on mount */ }, []);
useEffect(() => { document.addEventListener(...); return () => document.removeEventListener(...); }, [isOpen]);
```
```razor
@code {
    protected override async Task OnInitializedAsync() { /* load on mount */ }
    // cleanup → implement IDisposable / IAsyncDisposable and a Dispose method
}
```

`[]`-deps effect → `OnInitializedAsync`. Effects keyed on a changing param → `OnParametersSetAsync`. Cleanup function → `Dispose`.

### 10.4 Data fetching: Apollo `useQuery` → inject `HttpClient`

```tsx
const { data, loading, error } = useQuery<{ getMovie: Movie }>(GET_MOVIE, { variables: { id } });
const [shuffle, { data }] = useLazyQuery(SHUFFLE_MOVIE, { fetchPolicy: "network-only" });
```
```razor
@inject HttpClient Http
@code {
    private MovieSummary? movie; private bool loading = true;
    protected override async Task OnInitializedAsync()
    {
        movie = await new MovieApiClient(Http).GetDetailAsync(Id);  // GET /movies/{id}
        loading = false;
    }
}
```

There's no Apollo cache/normalized store in Blazor — you call the API and hold the result in a field. `loading`/`error` you track yourself (this repo added an `ApiCall.RunAsync` helper + error banner for that). `useMutation` + `refetchQueries` becomes "call the API, then re-run your load method."

### 10.5 Auth: NextAuth + `localStorage` → `AuthenticationStateProvider`

The TS client stashes the backend token and Apollo's `authLink` adds it to every request:

```tsx
const authLink = setContext((_, { headers }) => {
  const token = localStorage.getItem(AUTH_TOKEN_KEY);
  return { headers: { ...headers, authorization: token ? `Bearer ${token}` : "" } };
});
```

The Blazor app does the same with a `DelegatingHandler` (the direct analog of `authLink`) plus an `AuthenticationStateProvider` that decodes the JWT for `<AuthorizeView>` / `[Authorize]`:

```csharp
// BearerTokenHandler.cs — runs on every outgoing API call
var token = await tokens.GetAsync();                 // from localStorage via JS interop
if (token is not null)
    request.Headers.Authorization = new("Bearer", token);
```

| React/Next | Blazor |
|---|---|
| `useSession()` | `<AuthorizeView>` / inject `AuthenticationStateProvider` |
| in-component `if (!session) return <Login/>` | `[Authorize]` attribute + `AuthorizeRouteView` (auto-redirects) |
| `<SessionProvider>` / `<ApolloProvider>` wrappers | DI registration in `Program.cs` + `<CascadingAuthenticationState>` |

### 10.6 Routing

```
app/movie/[id]/page.tsx     → /movie/123
const { id } = useParams();
router.push("/profile");
```
```razor
@page "/movies/{Id:int}"            @* file-attribute route, with a typed constraint *@
@code { [Parameter] public int Id { get; set; } }
@inject NavigationManager Nav      // Nav.NavigateTo("/profile")
```

File-system routing → a `@page` directive in the component (route constraints like `:int` are built in). `useParams` → a `[Parameter]` matching the route token.

### 10.7 Controlled inputs

```tsx
<input value={query} onChange={(e) => setQuery(e.target.value)} />
```
```razor
<input @bind="query" @bind:event="oninput" />   @* two-way bind; oninput ≈ onChange-on-keystroke *@
```

### 10.8 Styling

The TS app uses Tailwind v4 + `class-variance-authority` + Radix (shadcn-style). This repo uses the stock **Bootstrap** that ships with the Blazor template plus component-scoped CSS (`MovieCard.razor.css`) — scoped CSS is built in (no CSS-modules setup). The CVA "variants" pattern doesn't have a direct equial; you'd pass parameters and compute classes.

---

## 11. Tooling, packages & tests

| | TS | C# |
|---|---|---|
| package manifest | `package.json` | `.csproj` (`<PackageReference>`) |
| lockfile | `pnpm-lock.yaml` | `packages.lock.json` (optional) |
| registry | npm | NuGet |
| run/build/test | `pnpm dev` / `build` / `test` | `dotnet run` / `build` / `test` |
| typecheck | `tsc --noEmit` | happens during `dotnet build` |
| format | Prettier (web only here) | `dotnet format` |
| lint | ESLint (web only) | Roslyn analyzers (in-build, `EnforceCodeStyleInBuild`) |
| codegen | `graphql-codegen` (typed ops) | source generators / OpenAPI (not used here) |

The starkest difference: **tests**. The original has *none* (no jest/vitest, `test` scripts absent, API `lint` is a stubbed `echo`). This rewrite has **184 xUnit tests**. A few mappings if you go looking:

| Testing need | TS equivalent | This repo (C#/xUnit) |
|---|---|---|
| test runner / asserts | jest / vitest | `xunit` (`[Fact]`, `[Theory]`, `Assert`) |
| mock an HTTP client | `nock` / `msw` | hand-rolled `StubHttpMessageHandler` |
| API integration test | `supertest` | `WebApplicationFactory<Program>` |
| throwaway database | test container / sqlite | EF Core **SQLite in-memory** |

```csharp
[Theory]
[InlineData(0)] [InlineData(11)]
public void Rating_outside_1_to_10_is_rejected(int value) { ... }  // ≈ it.each in jest
```

---

## 12. Deployment (FYI)

The TS app is deployed (Vercel = Next.js web, Render = Express API, `docker-compose` = local Postgres). This rewrite is **local-only** by design (a learning project) — there's no deploy target wired up, though the shape is the same: you'd host the API + Postgres somewhere and serve the Blazor static files from a CDN/host.

---

## 13. Things that surprised me coming from TS

- **No barrel-file imports needed for the framework.** `ImplicitUsings` + `global using` mean `System`, `System.Linq`, etc. are always in scope.
- **The build *is* the typecheck.** There's no separate `tsc` step; `dotnet build` fails on type errors and (here) on style/analyzer violations.
- **Nominal typing means refactors are safer but DTO duplication is real.** Two records with identical fields are *not* interchangeable — hence this repo's `MovieResponse` (API) vs `MovieSummary` (web client) vs `Movie` (Core), each mapped explicitly.
- **LINQ deferred execution + EF translation** is the one footgun: the same `.Where(...)` runs in-memory on a `List` but becomes SQL on a `DbSet`. Know which you're holding.
- **DI lifetimes replace "where do I `new` this?"** Once it clicks, the hand-built `context` object in the TS app reads as "manual DI."
- **`record` + pattern matching + switch expressions** cover most of what you'd reach union types for in TS, and read better for branching on shape.

---

*Generated as a learning aid for the Movie Night Picker .NET rewrite. Code references are real files in this repo and the sibling TypeScript repo.*

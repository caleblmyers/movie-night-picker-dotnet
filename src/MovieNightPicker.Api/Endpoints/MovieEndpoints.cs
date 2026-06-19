using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Core;
using MovieNightPicker.Core.Discovery;
using MovieNightPicker.Core.Models;
using MovieNightPicker.Core.Suggestions;
using MovieNightPicker.Tmdb;

namespace MovieNightPicker.Api.Endpoints;

/// <summary>
/// The read + suggest HTTP surface for movies: search, discover (shuffle),
/// detail, and the preference-driven suggestion endpoint.
/// </summary>
public static class MovieEndpoints
{
    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var movies = app.MapGroup("/movies");

        movies.MapGet("/search", SearchAsync).WithName("SearchMovies");
        movies.MapGet("/discover", DiscoverAsync).WithName("DiscoverMovies");
        movies.MapGet("/{id:int}", GetDetailAsync).WithName("GetMovie");
        movies.MapPost("/suggest", SuggestAsync).WithName("SuggestMovie");

        return app;
    }

    /// <summary>Full-text movie search (<c>GET /movies/search?query=&amp;page=</c>).</summary>
    private static async Task<IResult> SearchAsync(
        string? query, ITmdbClient client, CancellationToken ct, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new { error = "query is required" });
        }

        var options = new TmdbRequestOptions { Page = page < 1 ? 1 : page };
        var results = await client.SearchMoviesAsync(query, options, ct);
        return Results.Ok(MoviePageResponse.FromTmdb(results));
    }

    /// <summary>
    /// Shuffle discovery (<c>GET /movies/discover</c>). Binds a
    /// <see cref="DiscoverFilters"/> from query params, walks the progressive
    /// fallback chain, and returns the first non-empty page.
    /// </summary>
    private static async Task<IResult> DiscoverAsync(
        HttpRequest request, IMovieDataSource source, CancellationToken ct)
    {
        var filters = ParseFilters(request);

        // Walk the relaxation chain (a single step when no fallback is warranted),
        // returning the first step that yields anything.
        foreach (var query in FallbackChain.Build(filters))
        {
            var movies = await source.DiscoverMoviesAsync(query, ct);
            if (movies.Count > 0)
            {
                var results = movies.Select(MovieResponse.FromCore).ToList();
                return Results.Ok(results);
            }
        }

        return Results.Ok(Array.Empty<MovieResponse>());
    }

    /// <summary>Movie detail (<c>GET /movies/{id}</c>); 404 when TMDB has no such movie.</summary>
    private static async Task<IResult> GetDetailAsync(int id, ITmdbClient client, CancellationToken ct)
    {
        try
        {
            var movie = await client.GetMovieAsync(id, ct: ct);
            return Results.Ok(MovieResponse.FromTmdb(movie));
        }
        catch (TmdbApiException ex) when (ex.StatusCode == 404)
        {
            return Results.NotFound();
        }
    }

    /// <summary>
    /// Suggest a movie (<c>POST /movies/suggest</c>) from the user's picks: enrich
    /// each pick with TMDB credits + keywords, distil preferences, then run the
    /// recommendation cascade excluding the picks themselves.
    /// </summary>
    private static async Task<IResult> SuggestAsync(
        SuggestRequest body,
        IMovieDataSource source,
        ITmdbClient client,
        CancellationToken ct)
    {
        var ids = (body.SelectedMovieIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            return Results.BadRequest(new { error = "selectedMovieIds is required" });
        }

        // Enrich every pick concurrently (one task per id); Task.WhenAll preserves
        // input order, so filtering the skipped (null) picks keeps the original
        // ordering and downstream PreferenceExtractor/cascade behaviour.
        var enriched = (await Task.WhenAll(ids.Select(id => EnrichAsync(id, source, client, ct))))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        if (enriched.Count == 0)
        {
            return Results.NotFound();
        }

        var prefs = PreferenceExtractor.Extract(enriched);
        var suggestion = await RecommendationCascade.SuggestAsync(
            prefs, new HashSet<int>(ids), source, ct);

        return suggestion is null
            ? Results.NotFound()
            : Results.Ok(MovieResponse.FromCore(suggestion));
    }

    /// <summary>
    /// Enrich a single pick: resolve the movie (skip — null — if TMDB no longer
    /// knows it), then fetch its credits and keywords concurrently.
    /// </summary>
    private static async Task<PreferenceExtractor.SelectedMovie?> EnrichAsync(
        int id, IMovieDataSource source, ITmdbClient client, CancellationToken ct)
    {
        var movie = await source.GetMovieAsync(id, ct);
        if (movie is null)
        {
            return null; // skip picks TMDB no longer knows about
        }

        var creditsTask = client.GetMovieCreditsAsync(id, ct: ct);
        var keywordsTask = source.GetMovieKeywordsAsync(id, ct);
        await Task.WhenAll(creditsTask, keywordsTask);
        var credits = await creditsTask;
        var keywords = await keywordsTask;

        return new PreferenceExtractor.SelectedMovie(
            movie,
            keywords,
            credits.Cast.Select(c => c.Id).ToList(),
            credits.Crew.Select(c => new PreferenceExtractor.CrewMember(c.Id, c.Job ?? string.Empty)).ToList());
    }

    /// <summary>
    /// Builds a <see cref="DiscoverFilters"/> from the request query string.
    /// Repeated keys (e.g. <c>?genres=28&amp;genres=12</c>) become lists; range
    /// fields are paired <c>*Min</c>/<c>*Max</c> (or <c>*Start</c>/<c>*End</c>) keys.
    /// </summary>
    private static DiscoverFilters ParseFilters(HttpRequest request)
    {
        var q = request.Query;

        return new DiscoverFilters
        {
            Genres = Ints(q, "genres"),
            YearRange = Range(q, "yearStart", "yearEnd"),
            Era = Str(q, "era"),
            Cast = Ints(q, "cast"),
            Crew = Ints(q, "crew"),
            Keywords = Ints(q, "keywords"),
            Mood = Str(q, "mood"),
            RuntimeRange = Range(q, "runtimeMin", "runtimeMax"),
            WatchProviders = Str(q, "watchProviders"),
            ExcludeGenres = Ints(q, "excludeGenres"),
            ExcludeCast = Ints(q, "excludeCast"),
            ExcludeCrew = Ints(q, "excludeCrew"),
            PopularityRange = Range(q, "popularityMin", "popularityMax"),
            PopularityLevel = Str(q, "popularityLevel"),
            OriginCountries = Strings(q, "originCountries"),
        };
    }

    private static string? Str(IQueryCollection q, string key) =>
        q.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;

    private static IReadOnlyList<int> Ints(IQueryCollection q, string key) =>
        q.TryGetValue(key, out var v)
            ? v.SelectMany(s => (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                .Where(n => n is not null)
                .Select(n => n!.Value)
                .ToList()
            : [];

    private static IReadOnlyList<string> Strings(IQueryCollection q, string key) =>
        q.TryGetValue(key, out var v)
            ? v.SelectMany(s => (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToList()
            : [];

    private static (int Start, int End)? Range(IQueryCollection q, string startKey, string endKey)
    {
        var start = Str(q, startKey);
        var end = Str(q, endKey);
        return int.TryParse(start, out var s) && int.TryParse(end, out var e) ? (s, e) : null;
    }
}

using System.Net;
using System.Net.Http.Json;
using MovieNightPicker.Web.Models;

namespace MovieNightPicker.Web.Services;

/// <summary>
/// A page of search results mirroring the API's <c>MoviePageResponse</c>.
/// </summary>
public sealed record MoviePage(
    int Page,
    int TotalPages,
    int TotalResults,
    IReadOnlyList<MovieSummary> Results)
{
    /// <summary>An empty page — handy for the initial / no-query render.</summary>
    public static MoviePage Empty { get; } = new(0, 0, 0, []);
}

/// <summary>
/// Client-side discover (shuffle) filter inputs. Each field maps onto the API's
/// <c>ParseFilters</c> query keys; null/empty fields are simply omitted from the
/// query string. Numeric lists are sent as repeated comma-joined values.
/// </summary>
public sealed record DiscoverFilters
{
    public IReadOnlyList<int> Genres { get; init; } = [];
    public int? YearStart { get; init; }
    public int? YearEnd { get; init; }
    public string? Era { get; init; }
    public IReadOnlyList<int> Cast { get; init; } = [];
    public IReadOnlyList<int> Crew { get; init; } = [];
    public IReadOnlyList<int> Keywords { get; init; } = [];
    public string? Mood { get; init; }
    public int? RuntimeMin { get; init; }
    public int? RuntimeMax { get; init; }
    public string? WatchProviders { get; init; }
    public IReadOnlyList<int> ExcludeGenres { get; init; } = [];
    public int? PopularityMin { get; init; }
    public int? PopularityMax { get; init; }
    public string? PopularityLevel { get; init; }
    public IReadOnlyList<string> OriginCountries { get; init; } = [];
}

/// <summary>
/// Talks to the API's movie read endpoints (<c>/movies/search</c>,
/// <c>/movies/discover</c>, <c>/movies/{id}</c>). Pages construct one over the
/// injected <see cref="HttpClient"/>, which already carries the bearer token.
/// </summary>
public sealed class MovieApiClient(HttpClient http)
{
    /// <summary>Full-text search (<c>GET /movies/search?query=&amp;page=</c>).</summary>
    public async Task<MoviePage> SearchAsync(string query, int page = 1, CancellationToken ct = default)
    {
        var url = $"/movies/search?query={Uri.EscapeDataString(query)}&page={page}";
        var result = await http.GetFromJsonAsync<MoviePage>(url, ct);
        return result ?? MoviePage.Empty;
    }

    /// <summary>
    /// Shuffle discovery (<c>GET /movies/discover</c>). Builds the query string from
    /// the populated <paramref name="filters"/> fields and returns the API's flat
    /// list of movies (empty when nothing matched the fallback chain).
    /// </summary>
    public async Task<IReadOnlyList<MovieSummary>> ShuffleAsync(
        DiscoverFilters filters, CancellationToken ct = default)
    {
        var url = "/movies/discover" + BuildQuery(filters);
        var result = await http.GetFromJsonAsync<IReadOnlyList<MovieSummary>>(url, ct);
        return result ?? [];
    }

    /// <summary>Movie detail (<c>GET /movies/{id}</c>); null when TMDB has no such movie.</summary>
    public async Task<MovieSummary?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/movies/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MovieSummary>(ct);
    }

    /// <summary>
    /// Assembles the discover query string, emitting only populated fields under the
    /// keys the API's <c>ParseFilters</c> reads (comma-joined lists for repeated ids).
    /// </summary>
    private static string BuildQuery(DiscoverFilters f)
    {
        var parts = new List<string>();

        void AddInts(string key, IReadOnlyList<int> values)
        {
            if (values.Count > 0)
            {
                parts.Add($"{key}={Uri.EscapeDataString(string.Join(',', values))}");
            }
        }

        void AddStrings(string key, IReadOnlyList<string> values)
        {
            if (values.Count > 0)
            {
                parts.Add($"{key}={Uri.EscapeDataString(string.Join(',', values))}");
            }
        }

        void AddStr(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        void AddInt(string key, int? value)
        {
            if (value is { } v)
            {
                parts.Add($"{key}={v}");
            }
        }

        AddInts("genres", f.Genres);
        AddInt("yearStart", f.YearStart);
        AddInt("yearEnd", f.YearEnd);
        AddStr("era", f.Era);
        AddInts("cast", f.Cast);
        AddInts("crew", f.Crew);
        AddInts("keywords", f.Keywords);
        AddStr("mood", f.Mood);
        AddInt("runtimeMin", f.RuntimeMin);
        AddInt("runtimeMax", f.RuntimeMax);
        AddStr("watchProviders", f.WatchProviders);
        AddInts("excludeGenres", f.ExcludeGenres);
        AddInt("popularityMin", f.PopularityMin);
        AddInt("popularityMax", f.PopularityMax);
        AddStr("popularityLevel", f.PopularityLevel);
        AddStrings("originCountries", f.OriginCountries);

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}

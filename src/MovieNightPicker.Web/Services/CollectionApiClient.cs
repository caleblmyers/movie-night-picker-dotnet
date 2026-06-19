using System.Net;
using System.Net.Http.Json;

namespace MovieNightPicker.Web.Services;

/// <summary>A movie entry within a <see cref="CollectionResponse"/> (mirrors the API).</summary>
public sealed record CollectionMovie(int TmdbId, DateTime AddedAt);

/// <summary>A user's collection plus its movie entries (mirrors the API's CollectionResponse).</summary>
public sealed record CollectionResponse(
    int Id,
    string Name,
    string? Description,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CollectionMovie> Movies);

/// <summary>Request body for creating a collection.</summary>
public sealed record CreateCollectionRequest(string Name, string? Description, bool IsPublic = false);

/// <summary>Request body for adding a movie to a collection.</summary>
public sealed record AddMovieRequest(int TmdbId);

/// <summary>A {id, name, count} frequency row used by several insights tables.</summary>
public sealed record GenreCount(int GenreId, string GenreName, int Count);

public sealed record KeywordCount(int Id, string Name, int Count);

public sealed record PersonCount(int Id, string Name, string? ProfilePath, int Count);

/// <summary>The min/max release year across a collection's movies.</summary>
public sealed record YearRange(int Min, int Max);

/// <summary>
/// Aggregated insights for a collection (mirrors the API's CollectionInsightsResponse):
/// genre/keyword/actor/crew frequency tables plus year range and averages.
/// </summary>
public sealed record CollectionInsightsResult(
    int TotalMovies,
    int UniqueGenres,
    IReadOnlyList<GenreCount> MoviesByGenre,
    int UniqueKeywords,
    IReadOnlyList<KeywordCount> TopKeywords,
    int UniqueActors,
    IReadOnlyList<PersonCount> TopActors,
    int UniqueCrew,
    IReadOnlyList<PersonCount> TopCrew,
    YearRange? YearRange,
    double? AverageRuntime,
    double? AverageVoteAverage);

/// <summary>
/// Typed wrapper over the API's user-scoped <c>/collections</c> endpoints. The injected
/// <see cref="HttpClient"/> already carries the JWT bearer token, so every call is
/// authenticated as the current user. Pages construct this with <c>new CollectionApiClient(Http)</c>.
/// </summary>
public sealed class CollectionApiClient(HttpClient http)
{
    /// <summary>Lists the current user's collections (<c>GET /collections</c>).</summary>
    public async Task<IReadOnlyList<CollectionResponse>> ListAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<CollectionResponse>>("/collections", ct)
            ?? Array.Empty<CollectionResponse>();

    /// <summary>Gets one owned collection (<c>GET /collections/{id}</c>); null on 404.</summary>
    public async Task<CollectionResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/collections/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CollectionResponse>(ct);
    }

    /// <summary>Creates a collection (<c>POST /collections</c>); null when the request is rejected.</summary>
    public async Task<CollectionResponse?> CreateAsync(
        string name, string? description = null, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            "/collections", new CreateCollectionRequest(name, description), ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CollectionResponse>(ct)
            : null;
    }

    /// <summary>Deletes an owned collection (<c>DELETE /collections/{id}</c>); true on success.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/collections/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Adds a movie to a collection (<c>POST /collections/{id}/movies</c>); returns the updated collection.</summary>
    public async Task<CollectionResponse?> AddMovieAsync(int id, int tmdbId, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            $"/collections/{id}/movies", new AddMovieRequest(tmdbId), ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CollectionResponse>(ct)
            : null;
    }

    /// <summary>Removes a movie (<c>DELETE /collections/{id}/movies/{tmdbId}</c>); returns the updated collection.</summary>
    public async Task<CollectionResponse?> RemoveMovieAsync(int id, int tmdbId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/collections/{id}/movies/{tmdbId}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CollectionResponse>(ct)
            : null;
    }

    /// <summary>Aggregate insights for a collection (<c>GET /collections/{id}/insights</c>); null on 404.</summary>
    public async Task<CollectionInsightsResult?> GetInsightsAsync(int id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/collections/{id}/insights", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CollectionInsightsResult>(ct);
    }
}

using MovieNightPicker.Tmdb.Dtos;

namespace MovieNightPicker.Tmdb;

/// <summary>
/// Typed, read-only wrapper over the TMDB REST API v3. All methods are async and
/// accept a <see cref="CancellationToken"/>; the optional <see cref="TmdbRequestOptions"/>
/// supplies language / region / paging defaults when omitted.
/// </summary>
public interface ITmdbClient
{
    /// <summary>Full-text movie search (<c>/search/movie</c>).</summary>
    Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(
        string query, TmdbRequestOptions? options = null, CancellationToken ct = default);

    /// <summary>Filtered discovery (<c>/discover/movie</c>).</summary>
    Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(
        DiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default);

    /// <summary>Movie detail (<c>/movie/{id}</c>).</summary>
    Task<TmdbMovie> GetMovieAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default);

    /// <summary>Movie cast + crew (<c>/movie/{id}/credits</c>).</summary>
    Task<TmdbCredits> GetMovieCreditsAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default);

    /// <summary>Keywords attached to a movie (<c>/movie/{id}/keywords</c>).</summary>
    Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(
        int id, CancellationToken ct = default);

    /// <summary>The full movie genre list (<c>/genre/movie/list</c>).</summary>
    Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(
        TmdbRequestOptions? options = null, CancellationToken ct = default);

    /// <summary>Full-text people search (<c>/search/person</c>).</summary>
    Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(
        string query, TmdbRequestOptions? options = null, CancellationToken ct = default);

    /// <summary>Person detail (<c>/person/{id}</c>).</summary>
    Task<TmdbPerson> GetPersonAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default);

    /// <summary>A person's combined movie + TV credits (<c>/person/{id}/combined_credits</c>).</summary>
    Task<TmdbCredits> GetPersonCombinedCreditsAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default);
}

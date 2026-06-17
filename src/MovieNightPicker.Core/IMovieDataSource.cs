using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core;

/// <summary>
/// The Core engine's abstraction over movie data. Keeps the suggestion and
/// discovery logic independent of TMDB (or any concrete source) — the API layer
/// provides an adapter that fronts the TMDB typed client.
/// </summary>
public interface IMovieDataSource
{
    /// <summary>Run a discover query and return the matching movies.</summary>
    Task<IReadOnlyList<Movie>> DiscoverMoviesAsync(DiscoverParams p, CancellationToken ct = default);

    /// <summary>Fetch a single movie by id, or null if it doesn't exist.</summary>
    Task<Movie?> GetMovieAsync(int id, CancellationToken ct = default);

    /// <summary>Fetch the TMDB keyword ids associated with a movie.</summary>
    Task<IReadOnlyList<int>> GetMovieKeywordsAsync(int id, CancellationToken ct = default);
}

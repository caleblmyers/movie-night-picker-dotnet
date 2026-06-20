using CoreModels = MovieNightPicker.Core.Models;

namespace MovieNightPicker.Tmdb.Dtos;

/// <summary>
/// The single canonical mapping from a TMDB <see cref="TmdbMovie"/> DTO onto the
/// Core <see cref="CoreModels.Movie"/> domain model. The Tmdb project already
/// references Core, so returning a Core type here is fine and keeps every caller
/// (the API adapter, insights) from drifting on how a movie is mapped.
/// </summary>
public static class TmdbMovieMappingExtensions
{
    /// <summary>Maps a TMDB movie DTO onto the Core domain model.</summary>
    public static CoreModels.Movie ToCore(this TmdbMovie m) => new(
        m.Id,
        m.Title ?? string.Empty,
        m.Overview,
        m.PosterPath,
        ParseReleaseDate(m.ReleaseDate),
        m.VoteAverage,
        m.VoteCount,
        m.Runtime,
        m.Genres.Select(g => g.Id).ToList());

    /// <summary>Parses a TMDB <c>YYYY-MM-DD</c> release-date string; null/blank/garbage -> null.</summary>
    private static DateOnly? ParseReleaseDate(string? raw) =>
        DateOnly.TryParse(raw, out var date) ? date : null;
}

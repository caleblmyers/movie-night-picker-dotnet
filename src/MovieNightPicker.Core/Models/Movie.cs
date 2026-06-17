namespace MovieNightPicker.Core.Models;

/// <summary>
/// A movie as the Core engine sees it. Deliberately decoupled from any TMDB
/// DTO so the suggestion/discovery logic stays independent of the data source
/// (the API layer adapts TMDB to this in a later wave).
/// </summary>
public sealed record Movie(
    int Id,
    string Title,
    string? Overview,
    string? PosterPath,
    DateOnly? ReleaseDate,
    double? VoteAverage,
    int? VoteCount,
    int? Runtime,
    IReadOnlyList<int> Genres);

namespace MovieNightPicker.Web.Models;

/// <summary>
/// Client-side shape of a movie returned by the API (mirrors the API's
/// <c>MovieResponse</c>). Shared by every feature area so list/detail views and
/// the <c>MovieCard</c> component speak one vocabulary.
/// </summary>
public sealed record MovieSummary(
    int Id,
    string Title,
    string? Overview,
    string? PosterPath,
    string? ReleaseDate,
    double? VoteAverage,
    int? VoteCount,
    int? Runtime)
{
    /// <summary>TMDB poster URL for <see cref="PosterPath"/>, or null when there's no poster.</summary>
    public string? PosterUrl =>
        string.IsNullOrEmpty(PosterPath) ? null : $"https://image.tmdb.org/t/p/w342{PosterPath}";

    /// <summary>Release year parsed from the YYYY-MM-DD string, when present.</summary>
    public int? Year =>
        DateOnly.TryParse(ReleaseDate, out var d) ? d.Year : null;
}

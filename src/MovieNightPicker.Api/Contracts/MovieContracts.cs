using MovieNightPicker.Tmdb.Dtos;
using CoreModels = MovieNightPicker.Core.Models;

namespace MovieNightPicker.Api.Contracts;

/// <summary>A thin movie shape for list/detail responses — keeps TMDB DTOs out of the API surface.</summary>
public sealed record MovieResponse(
    int Id,
    string Title,
    string? Overview,
    string? PosterPath,
    string? ReleaseDate,
    double? VoteAverage,
    int? VoteCount,
    int? Runtime)
{
    /// <summary>Map a TMDB movie DTO onto the response shape.</summary>
    public static MovieResponse FromTmdb(TmdbMovie m) => new(
        m.Id, m.Title ?? string.Empty, m.Overview, m.PosterPath,
        m.ReleaseDate, m.VoteAverage, m.VoteCount, m.Runtime);

    /// <summary>Map a Core movie onto the response shape.</summary>
    public static MovieResponse FromCore(CoreModels.Movie m) => new(
        m.Id, m.Title, m.Overview, m.PosterPath,
        m.ReleaseDate?.ToString("yyyy-MM-dd"), m.VoteAverage, m.VoteCount, m.Runtime);
}

/// <summary>A page of movie results plus TMDB's paging metadata.</summary>
public sealed record MoviePageResponse(
    int Page,
    int TotalPages,
    int TotalResults,
    IReadOnlyList<MovieResponse> Results)
{
    public static MoviePageResponse FromTmdb(TmdbPagedResult<TmdbMovie> page) => new(
        page.Page, page.TotalPages, page.TotalResults,
        page.Results.Select(MovieResponse.FromTmdb).ToList());
}

/// <summary>A thin person shape for people search/detail responses.</summary>
public sealed record PersonResponse(
    int Id,
    string Name,
    string? ProfilePath,
    string? KnownForDepartment,
    string? Biography)
{
    public static PersonResponse FromTmdb(TmdbPerson p) => new(
        p.Id, p.Name ?? string.Empty, p.ProfilePath, p.KnownForDepartment, p.Biography);
}

/// <summary>A page of people results plus TMDB's paging metadata.</summary>
public sealed record PersonPageResponse(
    int Page,
    int TotalPages,
    int TotalResults,
    IReadOnlyList<PersonResponse> Results)
{
    public static PersonPageResponse FromTmdb(TmdbPagedResult<TmdbPerson> page) => new(
        page.Page, page.TotalPages, page.TotalResults,
        page.Results.Select(PersonResponse.FromTmdb).ToList());
}

/// <summary>Request body for <c>POST /movies/suggest</c>: the ids the user picked this session.</summary>
public sealed record SuggestRequest(IReadOnlyList<int> SelectedMovieIds);

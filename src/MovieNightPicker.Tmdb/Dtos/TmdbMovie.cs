using System.Text.Json.Serialization;

namespace MovieNightPicker.Tmdb.Dtos;

/// <summary>
/// A movie as returned by TMDB. Most fields are nullable because TMDB omits or
/// nulls them depending on the endpoint (e.g. <c>runtime</c> and <c>genres</c>
/// only appear on the movie-detail endpoint, not in discover/search results).
/// </summary>
public record TmdbMovie
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; init; }

    /// <summary>Raw TMDB release date string, e.g. <c>"2008-07-16"</c>.</summary>
    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("vote_average")]
    public double? VoteAverage { get; init; }

    [JsonPropertyName("vote_count")]
    public int? VoteCount { get; init; }

    [JsonPropertyName("runtime")]
    public int? Runtime { get; init; }

    /// <summary>Populated only on the movie-detail endpoint.</summary>
    [JsonPropertyName("genres")]
    public IReadOnlyList<TmdbGenre> Genres { get; init; } = [];
}

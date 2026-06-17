using System.Text.Json.Serialization;

namespace MovieNightPicker.Tmdb.Dtos;

/// <summary>A TMDB video (trailer, teaser, clip, …) attached to a movie.</summary>
public record TmdbVideo
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("site")]
    public string? Site { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("official")]
    public bool? Official { get; init; }

    [JsonPropertyName("published_at")]
    public string? PublishedAt { get; init; }
}

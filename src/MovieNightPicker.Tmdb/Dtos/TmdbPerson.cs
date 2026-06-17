using System.Text.Json.Serialization;

namespace MovieNightPicker.Tmdb.Dtos;

/// <summary>A person (actor / crew) as returned by TMDB's people endpoints.</summary>
public record TmdbPerson
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("biography")]
    public string? Biography { get; init; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }

    [JsonPropertyName("birthday")]
    public string? Birthday { get; init; }

    [JsonPropertyName("place_of_birth")]
    public string? PlaceOfBirth { get; init; }

    [JsonPropertyName("known_for_department")]
    public string? KnownForDepartment { get; init; }

    [JsonPropertyName("popularity")]
    public double? Popularity { get; init; }
}

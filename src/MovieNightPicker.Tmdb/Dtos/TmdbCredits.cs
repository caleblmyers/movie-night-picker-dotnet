using System.Text.Json.Serialization;

namespace MovieNightPicker.Tmdb.Dtos;

/// <summary>A member of a movie's cast.</summary>
public record TmdbCastMember
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("character")]
    public string? Character { get; init; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }

    [JsonPropertyName("order")]
    public int? Order { get; init; }
}

/// <summary>A member of a movie's crew (director, writer, …).</summary>
public record TmdbCrewMember
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("job")]
    public string? Job { get; init; }

    [JsonPropertyName("department")]
    public string? Department { get; init; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }
}

/// <summary>The cast + crew payload from TMDB's movie-credits endpoint.</summary>
public record TmdbCredits
{
    [JsonPropertyName("cast")]
    public IReadOnlyList<TmdbCastMember> Cast { get; init; } = [];

    [JsonPropertyName("crew")]
    public IReadOnlyList<TmdbCrewMember> Crew { get; init; } = [];
}

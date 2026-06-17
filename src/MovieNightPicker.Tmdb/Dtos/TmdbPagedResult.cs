using System.Text.Json.Serialization;

namespace MovieNightPicker.Tmdb.Dtos;

/// <summary>TMDB's standard paginated envelope for list endpoints.</summary>
/// <typeparam name="T">The element type contained in <see cref="Results"/>.</typeparam>
public record TmdbPagedResult<T>
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("results")]
    public IReadOnlyList<T> Results { get; init; } = [];

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; init; }
}

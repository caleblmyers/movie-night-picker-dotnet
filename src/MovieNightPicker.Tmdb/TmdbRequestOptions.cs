namespace MovieNightPicker.Tmdb;

/// <summary>
/// Common, cross-endpoint TMDB request knobs (region, language, paging, and the
/// optional quality / popularity floors that most list endpoints accept). Every
/// field has a sensible default so callers only set what they care about.
/// </summary>
public record TmdbRequestOptions
{
    public string Region { get; init; } = "US";

    public string Language { get; init; } = "en-US";

    public string SortBy { get; init; } = "popularity.desc";

    public int Page { get; init; } = 1;

    public int? Year { get; init; }

    public int? PrimaryReleaseYear { get; init; }

    public double? VoteAverageGte { get; init; }

    public int? VoteCountGte { get; init; }

    public string? WithOriginalLanguage { get; init; }

    public string? WithWatchProviders { get; init; }

    public bool IncludeAdult { get; init; }

    public double? PopularityGte { get; init; }

    public double? PopularityLte { get; init; }
}

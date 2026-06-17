namespace MovieNightPicker.Tmdb;

/// <summary>
/// The resolved, TMDB-shaped inputs to a <c>/discover/movie</c> call. These map
/// one-to-one onto TMDB query keys via <see cref="Internal.TmdbQueryStringBuilder"/>.
/// All collections default to empty and all ranges are nullable so an unset field
/// contributes no query key at all.
/// </summary>
public record DiscoverParams
{
    /// <summary>Inclusive [Start, End] release-year range.</summary>
    public (int Start, int End)? YearRange { get; init; }

    /// <summary>Inclusive [Start, End] runtime range in minutes.</summary>
    public (int Start, int End)? RuntimeRange { get; init; }

    /// <summary>Inclusive [Start, End] popularity range.</summary>
    public (double Start, double End)? PopularityRange { get; init; }

    public int[] Genres { get; init; } = [];

    public int[] Actors { get; init; } = [];

    public int[] Crew { get; init; } = [];

    public int[] Keywords { get; init; } = [];

    public int[] ExcludeGenres { get; init; } = [];

    public int[] ExcludeCast { get; init; } = [];

    public int[] ExcludeCrew { get; init; } = [];

    public string? WatchProviders { get; init; }

    public string[] OriginCountries { get; init; } = [];

    public double? VoteAverageGte { get; init; }

    public int? VoteCountGte { get; init; }
}

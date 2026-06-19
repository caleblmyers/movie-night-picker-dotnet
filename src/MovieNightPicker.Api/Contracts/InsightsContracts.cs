using MovieNightPicker.Core.Insights;

namespace MovieNightPicker.Api.Contracts;

/// <summary>The min/max release year across a collection's movies.</summary>
public sealed record YearRangeResponse(int Min, int Max);

/// <summary>
/// Aggregated insights for a collection: genre/keyword/actor/crew frequency
/// tables plus year-range and rating/runtime averages. Mirrors the Core
/// <see cref="CollectionInsightsResult"/> but flattens its tuple fields into
/// named records so the JSON shape is stable.
/// </summary>
public sealed record CollectionInsightsResponse(
    int TotalMovies,
    int UniqueGenres,
    IReadOnlyList<GenreCount> MoviesByGenre,
    int UniqueKeywords,
    IReadOnlyList<KeywordCount> TopKeywords,
    int UniqueActors,
    IReadOnlyList<ActorCount> TopActors,
    int UniqueCrew,
    IReadOnlyList<CrewCount> TopCrew,
    YearRangeResponse? YearRange,
    double? AverageRuntime,
    double? AverageVoteAverage)
{
    /// <summary>Map a Core <see cref="CollectionInsightsResult"/> onto the response shape.</summary>
    public static CollectionInsightsResponse FromResult(CollectionInsightsResult r) => new(
        r.TotalMovies,
        r.UniqueGenres,
        r.MoviesByGenre,
        r.UniqueKeywords,
        r.TopKeywords,
        r.UniqueActors,
        r.TopActors,
        r.UniqueCrew,
        r.TopCrew,
        r.YearRange is { } yr ? new YearRangeResponse(yr.Min, yr.Max) : null,
        r.AverageRuntime,
        r.AverageVoteAverage);
}

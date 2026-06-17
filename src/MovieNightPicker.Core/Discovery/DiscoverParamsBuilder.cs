using MovieNightPicker.Core.Constants;
using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core.Discovery;

/// <summary>
/// Converts the user-facing shuffle inputs (<see cref="DiscoverFilters"/>) into
/// a resolved, TMDB-shaped <see cref="DiscoverParams"/>. Pure function — no I/O.
/// </summary>
public static class DiscoverParamsBuilder
{
    public static DiscoverParams Build(DiscoverFilters filters)
    {
        // Era resolves to a year range, but only when the user didn't pin an
        // explicit YearRange (an explicit range always wins).
        var yearRange = filters.YearRange
            ?? (filters.Era is not null ? EraYearRanges.RangeFor(filters.Era) : null);

        // Explicit keywords plus any keyword ids the chosen mood expands to,
        // merged and de-duplicated.
        var moodKeywords = filters.Mood is not null
            ? MoodKeywords.KeywordsFor(filters.Mood)
            : [];
        var keywords = filters.Keywords
            .Concat(moodKeywords)
            .Distinct()
            .ToArray();

        // A named popularity level resolves to a range, but an explicit
        // PopularityRange always wins.
        var popularityRange = filters.PopularityRange
            ?? (filters.PopularityLevel is not null ? PopularityLevels.RangeFor(filters.PopularityLevel) : null);

        return new DiscoverParams
        {
            Genres = filters.Genres,
            YearRange = yearRange,
            Actors = filters.Cast,
            Crew = filters.Crew,
            Keywords = keywords,
            RuntimeRange = filters.RuntimeRange,
            WatchProviders = filters.WatchProviders,
            ExcludeGenres = filters.ExcludeGenres,
            ExcludeCast = filters.ExcludeCast,
            ExcludeCrew = filters.ExcludeCrew,
            PopularityRange = popularityRange,
            OriginCountries = filters.OriginCountries,
        };
    }
}

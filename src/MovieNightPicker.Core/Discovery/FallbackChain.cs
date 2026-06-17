using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core.Discovery;

/// <summary>
/// Builds the progressive-relaxation chain for a shuffle query: an ordered list
/// of <see cref="DiscoverParams"/> from the most specific to the loosest, so a
/// caller can walk it until one yields results.
/// </summary>
/// <remarks>
/// Only soft filters are relaxed. The following are <em>strict</em> and survive
/// every step: an explicit <see cref="DiscoverFilters.YearRange"/>,
/// <see cref="DiscoverFilters.RuntimeRange"/>, vote-average/vote-count floors,
/// <see cref="DiscoverFilters.WatchProviders"/>, all <c>Exclude*</c> filters,
/// <see cref="DiscoverFilters.OriginCountries"/>, and explicit
/// <see cref="DiscoverFilters.Keywords"/>. (Era-derived year ranges and
/// mood-derived keywords are soft and may be dropped.)
/// </remarks>
public static class FallbackChain
{
    /// <summary>
    /// A fallback chain is only worth building when the query is specific enough
    /// to plausibly return nothing — multiple genres, or any cast/crew filter.
    /// A simple single-genre browse needs no relaxation.
    /// </summary>
    public static bool ShouldTryFallback(DiscoverFilters filters) =>
        filters.Genres.Count > 1 || filters.Cast.Count > 0 || filters.Crew.Count > 0;

    public static IReadOnlyList<DiscoverParams> Build(DiscoverFilters filters)
    {
        if (!ShouldTryFallback(filters))
        {
            return [DiscoverParamsBuilder.Build(filters)];
        }

        var firstGenre = filters.Genres.Take(1).ToArray();

        // Each step is the original filters with progressively more soft filters
        // dropped. Strict filters are never touched, so they ride through.
        var steps = new List<DiscoverFilters>
        {
            filters,                                                  // 1. full params
            filters with { Mood = null },                            // 2. drop mood
        };

        // 3. drop crew first, but only when actors are also present
        if (filters.Cast.Count > 0)
        {
            steps.Add(filters with { Mood = null, Crew = [] });
        }

        // 4. drop actors (and crew)
        steps.Add(filters with { Mood = null, Cast = [], Crew = [] });

        // 5. reduce to a single genre
        steps.Add(filters with { Mood = null, Cast = [], Crew = [], Genres = firstGenre });

        // 6. drop the era-derived year range (explicit YearRange is strict and stays)
        steps.Add(filters with { Mood = null, Cast = [], Crew = [], Genres = firstGenre, Era = null });

        // 7. genre only
        steps.Add(filters with { Mood = null, Cast = [], Crew = [], Genres = firstGenre, Era = null });

        // 8. year range / era only — drop genres entirely
        steps.Add(filters with { Mood = null, Cast = [], Crew = [], Genres = [] });

        return DedupConsecutive(steps.Select(DiscoverParamsBuilder.Build)).ToList();
    }

    /// <summary>
    /// Collapse runs of identical param sets — several relaxation steps coincide
    /// when a query has few soft filters to drop. Uses <see cref="DiscoverParams"/>'s
    /// structural value equality.
    /// </summary>
    private static IEnumerable<DiscoverParams> DedupConsecutive(IEnumerable<DiscoverParams> source)
    {
        DiscoverParams? previous = null;
        foreach (var current in source)
        {
            if (previous is null || !previous.Equals(current))
            {
                yield return current;
            }

            previous = current;
        }
    }
}

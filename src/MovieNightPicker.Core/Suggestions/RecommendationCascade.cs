using MovieNightPicker.Core.Constants;
using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core.Suggestions;

/// <summary>
/// The crown-jewel recommendation logic: given a preference profile, try five
/// increasingly broad discover strategies in order, returning the first that
/// yields a movie the user hasn't already seen. Falls back to a relaxed
/// emergency query if all five come up empty.
/// </summary>
public static class RecommendationCascade
{
    private const string QualitySort = "vote_average.desc";
    private const string PopularitySort = "popularity.desc";
    private const int CandidatePoolSize = 5;

    /// <summary>
    /// Run the cascade. <paramref name="excludeIds"/> are movies to never
    /// suggest (watch history plus the picks themselves). <paramref name="selector"/>
    /// chooses one movie from the top candidates of the winning strategy —
    /// defaults to the first (highest-rated), and is injectable so tests stay
    /// deterministic. Returns null only if even the emergency query is empty.
    /// </summary>
    public static async Task<Movie?> SuggestAsync(
        MoviePreferences prefs,
        ISet<int> excludeIds,
        IMovieDataSource source,
        CancellationToken ct = default,
        Func<IReadOnlyList<Movie>, Movie>? selector = null)
    {
        var pick = selector ?? (candidates => candidates[0]);

        var topTwoGenres = prefs.Genres.Take(2).ToArray();
        var topGenre = prefs.Genres.Take(1).ToArray();
        var topKeywords = prefs.KeywordIds.Take(3).ToArray();

        // Five strategies, broadening as they go. Each is gated at the standard
        // 6.5/150 quality floor and sorted best-rated first.
        var strategies = new[]
        {
            QualityStrategy(topTwoGenres, [], prefs.YearRange),  // 1. top 2 genres + year range
            QualityStrategy(topGenre, topKeywords, null),        // 2. top genre + top 3 keywords
            QualityStrategy(topGenre, [], prefs.YearRange),      // 3. top genre + year range
            QualityStrategy(topTwoGenres, [], null),             // 4. top 2 genres only
            QualityStrategy([], [], prefs.YearRange),            // 5. year range only
        };

        foreach (var strategy in strategies)
        {
            var chosen = await TryAsync(strategy);
            if (chosen is not null)
            {
                return chosen;
            }
        }

        // Emergency: relax the quality floor (6.0/100) and rank by popularity to
        // surface *something* watchable.
        var emergency = new DiscoverParams
        {
            Genres = topTwoGenres,
            YearRange = prefs.YearRange,
            VoteAverageGte = QualityFloors.Emergency.VoteAverageGte,
            VoteCountGte = QualityFloors.Emergency.VoteCountGte,
            SortBy = PopularitySort,
        };

        return await TryAsync(emergency);

        // Local: build a quality-floored strategy.
        static DiscoverParams QualityStrategy(
            IReadOnlyList<int> genres, IReadOnlyList<int> keywords, (int Start, int End)? yearRange) =>
            new()
            {
                Genres = genres,
                Keywords = keywords,
                YearRange = yearRange,
                VoteAverageGte = QualityFloors.CascadeDefault.VoteAverageGte,
                VoteCountGte = QualityFloors.CascadeDefault.VoteCountGte,
                SortBy = QualitySort,
            };

        // Local: run one strategy, drop excluded movies, pick from the top pool.
        async Task<Movie?> TryAsync(DiscoverParams parameters)
        {
            var results = await source.DiscoverMoviesAsync(parameters, ct);
            var candidates = results
                .Where(m => !excludeIds.Contains(m.Id))
                .Take(CandidatePoolSize)
                .ToList();

            return candidates.Count > 0 ? pick(candidates) : null;
        }
    }
}

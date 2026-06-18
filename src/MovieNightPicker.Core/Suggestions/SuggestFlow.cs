using MovieNightPicker.Core.Constants;
using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core.Suggestions;

/// <summary>
/// Drives one round of the 10-round suggest flow: turns a
/// <see cref="CategoryRoundDef"/>'s four slots into four concrete movies, fetched
/// from an <see cref="IMovieDataSource"/>, with per-slot relaxation, cross-slot
/// dedup, exclusion of already-seen picks, and a relaxed fill-up so a round
/// always offers four choices when the data source can supply them.
/// </summary>
public static class SuggestFlow
{
    private const string PopularitySort = "popularity.desc";
    private const int SlotsPerRound = 4;

    /// <summary>
    /// Produce the four movies for <paramref name="round"/> (1-10).
    /// <paramref name="selectedMovieIds"/> are the user's picks so far — excluded
    /// from results and, on round 10, mined for the most-frequent genre that
    /// anchors the Mixed round. <paramref name="slotSelector"/> chooses one movie
    /// from a slot's candidates (default: the first, highest-ranked) and is
    /// injectable so tests stay deterministic — the flow never uses
    /// <see cref="Random"/>.
    /// </summary>
    public static async Task<SuggestRoundResult> GetRoundAsync(
        int round,
        IReadOnlyList<int> selectedMovieIds,
        IMovieDataSource source,
        CancellationToken ct = default,
        Func<IReadOnlyList<Movie>, Movie>? slotSelector = null)
    {
        if (round is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(round), round, "Round must be between 1 and 10.");
        }

        var select = slotSelector ?? (candidates => candidates[0]);
        var excluded = new HashSet<int>(selectedMovieIds);

        // Only the Mixed round needs to know the user's taste; skip the lookups
        // (and the extra data-source calls) for every other round.
        var anchorGenre = round == 10
            ? await InferAnchorGenreAsync(selectedMovieIds, source, ct)
            : (int?)null;

        var def = SuggestRoundGenerator.Generate(round, anchorGenre);

        var taken = new HashSet<int>();
        var picks = new List<Movie>(SlotsPerRound);
        foreach (var slot in def.Slots)
        {
            var pick = await FetchSlotAsync(slot, def, excluded, taken, source, select, ct);
            if (pick is not null)
            {
                taken.Add(pick.Id);
                picks.Add(pick);
            }
        }

        if (picks.Count < SlotsPerRound)
        {
            await FillToFourAsync(def, excluded, taken, picks, source, ct);
        }

        return new SuggestRoundResult(picks, def.Category, def.CategoryLabel);
    }

    /// <summary>
    /// Fetch one movie for a slot, relaxing in three steps when a query is empty:
    /// drop the mood keywords, then the year range (genre + popularity only), then
    /// fall back to genre-only at the desperate quality floor.
    /// </summary>
    private static async Task<Movie?> FetchSlotAsync(
        SlotDefinition slot,
        CategoryRoundDef def,
        ISet<int> excluded,
        ISet<int> taken,
        IMovieDataSource source,
        Func<IReadOnlyList<Movie>, Movie> select,
        CancellationToken ct)
    {
        var attempts = new[]
        {
            SlotParams(slot, def, dropKeywords: false, dropYearRange: false),
            SlotParams(slot, def, dropKeywords: true, dropYearRange: false),
            SlotParams(slot, def, dropKeywords: true, dropYearRange: true),
            new DiscoverParams
            {
                Genres = slot.Genres,
                PopularityRange = slot.PopularityRange,
                VoteAverageGte = QualityFloors.Desperate.VoteAverageGte,
                VoteCountGte = QualityFloors.Desperate.VoteCountGte,
                SortBy = def.DefaultSortBy,
            },
        };

        foreach (var parameters in attempts)
        {
            var pick = await PickOneAsync(parameters, excluded, taken, source, select, ct);
            if (pick is not null)
            {
                return pick;
            }
        }

        return null;
    }

    /// <summary>Build a slot's discover query, optionally dropping its soft filters.</summary>
    private static DiscoverParams SlotParams(
        SlotDefinition slot, CategoryRoundDef def, bool dropKeywords, bool dropYearRange) =>
        new()
        {
            Genres = slot.Genres,
            YearRange = dropYearRange ? null : slot.YearRange,
            Keywords = dropKeywords ? [] : slot.KeywordIds,
            PopularityRange = slot.PopularityRange,
            VoteAverageGte = slot.VoteAverageGte ?? def.DefaultVoteAverageGte,
            VoteCountGte = slot.VoteCountGte ?? def.DefaultVoteCountGte,
            SortBy = def.DefaultSortBy,
        };

    /// <summary>Run a query, drop excluded/already-taken movies, pick one or null.</summary>
    private static async Task<Movie?> PickOneAsync(
        DiscoverParams parameters,
        ISet<int> excluded,
        ISet<int> taken,
        IMovieDataSource source,
        Func<IReadOnlyList<Movie>, Movie> select,
        CancellationToken ct)
    {
        var results = await source.DiscoverMoviesAsync(parameters, ct);
        var candidates = results
            .Where(m => !excluded.Contains(m.Id) && !taken.Contains(m.Id))
            .ToList();

        return candidates.Count > 0 ? select(candidates) : null;
    }

    /// <summary>
    /// Top the round up to four movies with a single broad query (anchor genre at
    /// the desperate floor, ranked by popularity) when slots came up short.
    /// </summary>
    private static async Task FillToFourAsync(
        CategoryRoundDef def,
        ISet<int> excluded,
        ISet<int> taken,
        List<Movie> picks,
        IMovieDataSource source,
        CancellationToken ct)
    {
        var fillGenre = def.AnchorGenre ?? def.Slots.SelectMany(s => s.Genres).FirstOrDefault();
        var fill = new DiscoverParams
        {
            Genres = fillGenre > 0 ? [fillGenre] : [],
            VoteAverageGte = QualityFloors.Desperate.VoteAverageGte,
            VoteCountGte = QualityFloors.Desperate.VoteCountGte,
            SortBy = PopularitySort,
        };

        var results = await source.DiscoverMoviesAsync(fill, ct);
        foreach (var movie in results)
        {
            if (picks.Count >= SlotsPerRound)
            {
                break;
            }

            if (excluded.Contains(movie.Id) || taken.Contains(movie.Id))
            {
                continue;
            }

            taken.Add(movie.Id);
            picks.Add(movie);
        }
    }

    /// <summary>
    /// The most frequent genre across the user's picks (id ascending as a
    /// deterministic tie-break), or null when no pick resolves or carries a genre.
    /// </summary>
    private static async Task<int?> InferAnchorGenreAsync(
        IReadOnlyList<int> selectedMovieIds, IMovieDataSource source, CancellationToken ct)
    {
        var counts = new Dictionary<int, int>();
        foreach (var id in selectedMovieIds)
        {
            var movie = await source.GetMovieAsync(id, ct);
            if (movie is null)
            {
                continue;
            }

            foreach (var genre in movie.Genres)
            {
                counts[genre] = counts.GetValueOrDefault(genre) + 1;
            }
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => (int?)pair.Key)
            .FirstOrDefault();
    }
}

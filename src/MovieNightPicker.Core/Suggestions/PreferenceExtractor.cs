using MovieNightPicker.Core.Constants;
using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core.Suggestions;

/// <summary>
/// Distils a <see cref="MoviePreferences"/> profile from the movies a user
/// picked during the suggest flow, by counting feature frequencies and keeping
/// the ones that clear a confidence threshold. Pure function — no I/O.
/// </summary>
public static class PreferenceExtractor
{
    /// <summary>
    /// A picked movie enriched with the credit/keyword data a bare
    /// <see cref="Movie"/> doesn't carry. The API layer builds these from TMDB
    /// credits + keywords; the extractor stays pure over what it's given.
    /// </summary>
    public sealed record SelectedMovie(
        Movie Movie,
        IReadOnlyList<int> KeywordIds,
        IReadOnlyList<int> ActorIds,
        IReadOnlyList<CrewMember> Crew);

    /// <summary>A crew credit — the job is what lets us keep directors/writers only.</summary>
    public sealed record CrewMember(int Id, string Job);

    private static readonly HashSet<string> DirectingWritingJobs =
        new(StringComparer.OrdinalIgnoreCase) { "Director", "Writer" };

    /// <summary>
    /// Extract preferences from bare movies. Only genres and a year range can be
    /// derived from what <see cref="Movie"/> exposes.
    /// </summary>
    /// <remarks>
    /// TODO: keywords, actors, and crew require credit/keyword data not present
    /// on <see cref="Movie"/> — use the <see cref="SelectedMovie"/> overload once
    /// the API adapter enriches picks with TMDB credits.
    /// </remarks>
    public static MoviePreferences Extract(IReadOnlyList<Movie> selected)
    {
        var total = selected.Count;
        return new MoviePreferences
        {
            Genres = TopFeatures(selected.SelectMany(m => m.Genres), total, take: 3, alwaysIncludeMostFrequent: true),
            YearRange = YearRangeOf(selected),
        };
    }

    /// <summary>Extract the full preference profile from enriched picks.</summary>
    public static MoviePreferences Extract(IReadOnlyList<SelectedMovie> selected)
    {
        var total = selected.Count;
        var directorsAndWriters = selected
            .SelectMany(s => s.Crew)
            .Where(c => DirectingWritingJobs.Contains(c.Job))
            .Select(c => c.Id);

        return new MoviePreferences
        {
            Genres = TopFeatures(selected.SelectMany(s => s.Movie.Genres), total, take: 3, alwaysIncludeMostFrequent: true),
            KeywordIds = TopFeatures(selected.SelectMany(s => s.KeywordIds), total, take: 5, alwaysIncludeMostFrequent: false),
            Actors = TopFeatures(selected.SelectMany(s => s.ActorIds), total, take: 2, alwaysIncludeMostFrequent: false),
            Crew = TopFeatures(directorsAndWriters, total, take: 2, alwaysIncludeMostFrequent: false),
            YearRange = YearRangeOf(selected.Select(s => s.Movie)),
        };
    }

    /// <summary>
    /// The confidence threshold: with three or fewer picks a single occurrence
    /// is enough; beyond that a feature must appear in at least a quarter of them.
    /// </summary>
    private static int ConfidenceThreshold(int total) =>
        total <= 3 ? 1 : (int)Math.Ceiling(0.25 * total);

    /// <summary>
    /// Rank ids by frequency (id ascending as a deterministic tie-break), keep
    /// up to <paramref name="take"/> that clear the threshold. When nothing
    /// clears it and <paramref name="alwaysIncludeMostFrequent"/> is set, keep
    /// the single most frequent id anyway.
    /// </summary>
    private static IReadOnlyList<int> TopFeatures(
        IEnumerable<int> ids, int total, int take, bool alwaysIncludeMostFrequent)
    {
        var ranked = ids
            .GroupBy(id => id)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Id)
            .ToList();

        var threshold = ConfidenceThreshold(total);
        var kept = ranked
            .Where(x => x.Count >= threshold)
            .Take(take)
            .Select(x => x.Id)
            .ToList();

        if (kept.Count == 0 && alwaysIncludeMostFrequent && ranked.Count > 0)
        {
            kept.Add(ranked[0].Id);
        }

        return kept;
    }

    /// <summary>
    /// The span of release years across the picks, widened by an expansion of
    /// <c>max(5, ceil(0.5 * span))</c> on each side and clamped to
    /// [1900, current year]. Null when no pick has a release date.
    /// </summary>
    private static (int Start, int End)? YearRangeOf(IEnumerable<Movie> selected)
    {
        var years = selected
            .Where(m => m.ReleaseDate is not null)
            .Select(m => m.ReleaseDate!.Value.Year)
            .ToList();

        if (years.Count == 0)
        {
            return null;
        }

        var min = years.Min();
        var max = years.Max();
        var expansion = Math.Max(5, (int)Math.Ceiling(0.5 * (max - min)));

        var start = Math.Clamp(min - expansion, 1900, EraYearRanges.CurrentYear);
        var end = Math.Clamp(max + expansion, 1900, EraYearRanges.CurrentYear);
        return (start, end);
    }
}

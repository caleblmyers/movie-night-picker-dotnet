using MovieNightPicker.Core.Models;
using MovieNightPicker.Core.Suggestions;

namespace MovieNightPicker.Tests.Core;

public class PreferenceExtractorTests
{
    private static Movie MovieWith(int id, int[] genres, int? year = null) =>
        new(id, $"Movie {id}", null, null,
            year is null ? null : new DateOnly(year.Value, 1, 1),
            null, null, null, genres);

    [Fact]
    public void Few_picks_keep_any_genre_seen_at_least_once()
    {
        // 3 picks -> threshold is 1, so every distinct genre clears it (top 3).
        var picks = new[]
        {
            MovieWith(1, [28, 12]),
            MovieWith(2, [28]),
            MovieWith(3, [16]),
        };

        var prefs = PreferenceExtractor.Extract(picks);

        // ranked by frequency: 28 (x2), then 12 & 16 (x1, id-tiebreak) -> top 3
        Assert.Equal([28, 12, 16], prefs.Genres);
    }

    [Fact]
    public void Many_picks_apply_the_quarter_threshold()
    {
        // 8 picks -> threshold = ceil(0.25*8) = 2. Only genre 28 (x3) clears it.
        var picks = new List<Movie>
        {
            MovieWith(1, [28]), MovieWith(2, [28]), MovieWith(3, [28]),
            MovieWith(4, [12]), MovieWith(5, [16]), MovieWith(6, [35]),
            MovieWith(7, [80]), MovieWith(8, [99]),
        };

        var prefs = PreferenceExtractor.Extract(picks);

        Assert.Equal([28], prefs.Genres);
    }

    [Fact]
    public void Most_frequent_genre_is_kept_even_if_below_threshold()
    {
        // 8 single-genre picks, all distinct -> nobody clears threshold 2,
        // but the most frequent (lowest id on the tie) is still kept.
        var picks = new List<Movie>
        {
            MovieWith(1, [99]), MovieWith(2, [80]), MovieWith(3, [35]),
            MovieWith(4, [28]), MovieWith(5, [16]), MovieWith(6, [14]),
            MovieWith(7, [12]), MovieWith(8, [18]),
        };

        var prefs = PreferenceExtractor.Extract(picks);

        // all tie at count 1 -> lowest id 12 wins the single forced slot
        Assert.Equal([12], prefs.Genres);
    }

    [Fact]
    public void Genres_are_capped_at_top_three()
    {
        var picks = new[]
        {
            MovieWith(1, [28, 12, 16, 35, 80]),
            MovieWith(2, [28, 12, 16, 35, 80]),
        };

        var prefs = PreferenceExtractor.Extract(picks);

        Assert.Equal(3, prefs.Genres.Count);
        Assert.Equal(new[] { 12, 16, 28 }, prefs.Genres.OrderBy(x => x).ToArray()); // top 3 by id-tiebreak
    }

    [Fact]
    public void Year_range_expands_by_half_the_span_minimum_five()
    {
        var picks = new[]
        {
            MovieWith(1, [28], year: 2000),
            MovieWith(2, [28], year: 2010),
        };

        var prefs = PreferenceExtractor.Extract(picks);

        // span 10 -> expansion max(5, ceil(5)) = 5 -> [1995, 2015]
        Assert.Equal((1995, 2015), prefs.YearRange);
    }

    [Fact]
    public void Year_range_uses_minimum_expansion_of_five_for_narrow_spans()
    {
        var picks = new[]
        {
            MovieWith(1, [28], year: 2008),
            MovieWith(2, [28], year: 2010),
        };

        var prefs = PreferenceExtractor.Extract(picks);

        // span 2 -> expansion max(5, ceil(1)) = 5 -> [2003, 2015]
        Assert.Equal((2003, 2015), prefs.YearRange);
    }

    [Fact]
    public void Year_range_is_null_when_no_release_dates()
    {
        var prefs = PreferenceExtractor.Extract([MovieWith(1, [28])]);

        Assert.Null(prefs.YearRange);
    }

    [Fact]
    public void Enriched_extract_picks_top_keywords_actors_and_crew()
    {
        var picks = new[]
        {
            new PreferenceExtractor.SelectedMovie(
                MovieWith(1, [28]),
                KeywordIds: [500, 600],
                ActorIds: [10, 20],
                Crew: [new(5, "Director"), new(99, "Editor")]),
            new PreferenceExtractor.SelectedMovie(
                MovieWith(2, [28]),
                KeywordIds: [500, 700],
                ActorIds: [10, 30],
                Crew: [new(5, "Director"), new(99, "Editor")]),
        };

        var prefs = PreferenceExtractor.Extract(picks);

        // keyword 500 appears twice -> ranked first; threshold 1 keeps all, top 5
        Assert.Contains(500, prefs.KeywordIds);
        Assert.Equal(500, prefs.KeywordIds[0]);

        // actor 10 appears twice -> top 2 keeps it first
        Assert.Equal(10, prefs.Actors[0]);
        Assert.True(prefs.Actors.Count <= 2);

        // only directors/writers count -> the "Editor" (id 99) is excluded
        Assert.DoesNotContain(99, prefs.Crew);
        Assert.True(prefs.Crew.Count <= 2);
    }
}

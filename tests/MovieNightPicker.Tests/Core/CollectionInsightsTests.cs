using MovieNightPicker.Core.Insights;
using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Tests.Core;

public class CollectionInsightsTests
{
    private static Movie Mv(int id, int[]? genres = null, int? year = null, int? runtime = null, double? vote = null) =>
        new(id, $"M{id}", null, null,
            year is null ? null : new DateOnly(year.Value, 1, 1),
            vote, 100, runtime, genres ?? []);

    private static InsightsMovie IM(
        Movie movie,
        (int Id, string Name)[]? keywords = null,
        (int Id, string Name, string? ProfilePath)[]? cast = null,
        (int Id, string Name, string? ProfilePath, string Job, string Department)[]? crew = null) =>
        new(movie, keywords ?? [], cast ?? [], crew ?? []);

    [Fact]
    public void Empty_collection_yields_a_zeroed_result_without_throwing()
    {
        var result = CollectionInsights.Compute([]);

        Assert.Equal(0, result.TotalMovies);
        Assert.Equal(0, result.UniqueGenres);
        Assert.Empty(result.MoviesByGenre);
        Assert.Equal(0, result.UniqueKeywords);
        Assert.Empty(result.TopKeywords);
        Assert.Empty(result.TopActors);
        Assert.Empty(result.TopCrew);
        Assert.Null(result.YearRange);
        Assert.Null(result.AverageRuntime);
        Assert.Null(result.AverageVoteAverage);
    }

    [Fact]
    public void Genres_are_counted_named_and_ordered_by_frequency_then_id()
    {
        var movies = new[]
        {
            IM(Mv(1, [28, 18])),
            IM(Mv(2, [28])),
            IM(Mv(3, [35])),
        };

        var result = CollectionInsights.Compute(movies);

        Assert.Equal(3, result.TotalMovies);
        Assert.Equal(3, result.UniqueGenres);

        Assert.Equal(28, result.MoviesByGenre[0].GenreId);
        Assert.Equal("Action", result.MoviesByGenre[0].GenreName);
        Assert.Equal(2, result.MoviesByGenre[0].Count);

        // 18 and 35 both have count 1 -> id ascending tie-break puts 18 first.
        Assert.Equal(18, result.MoviesByGenre[1].GenreId);
        Assert.Equal("Drama", result.MoviesByGenre[1].GenreName);
        Assert.Equal(35, result.MoviesByGenre[2].GenreId);
    }

    [Fact]
    public void Unknown_genre_ids_fall_back_to_a_placeholder_name()
    {
        var result = CollectionInsights.Compute([IM(Mv(1, [424242]))]);

        Assert.Equal("Unknown", result.MoviesByGenre[0].GenreName);
    }

    [Fact]
    public void Keywords_are_truncated_to_the_top_twenty_by_count()
    {
        // Keyword j appears in movies 1..j, so its count is j (25 = most frequent).
        var movies = Enumerable.Range(1, 25)
            .Select(i => IM(Mv(i),
                keywords: Enumerable.Range(i, 25 - i + 1).Select(j => (j, $"kw{j}")).ToArray()))
            .ToArray();

        var result = CollectionInsights.Compute(movies);

        Assert.Equal(25, result.UniqueKeywords);
        Assert.Equal(20, result.TopKeywords.Count);
        Assert.Equal(25, result.TopKeywords[0].Id);
        Assert.Equal(25, result.TopKeywords[0].Count);
        Assert.Equal("kw25", result.TopKeywords[0].Name);
    }

    [Fact]
    public void Actors_are_truncated_to_the_top_ten_and_carry_profile_paths()
    {
        var movies = Enumerable.Range(1, 12)
            .Select(i => IM(Mv(i),
                cast: Enumerable.Range(i, 12 - i + 1)
                    .Select(j => (j, $"actor{j}", (string?)$"/p{j}.jpg")).ToArray()))
            .ToArray();

        var result = CollectionInsights.Compute(movies);

        Assert.Equal(12, result.UniqueActors);
        Assert.Equal(10, result.TopActors.Count);
        Assert.Equal(12, result.TopActors[0].Id);
        Assert.Equal("/p12.jpg", result.TopActors[0].ProfilePath);
        Assert.Equal(12, result.TopActors[0].Count);
    }

    [Fact]
    public void Crew_is_filtered_to_directors_and_writers_by_job_or_department()
    {
        var crew = new (int, string, string?, string, string)[]
        {
            (1, "Dir", null, "Director", "Directing"),    // kept (job + dept)
            (2, "Novelist", null, "Novel", "Writing"),    // kept via department
            (3, "Editor", null, "Editor", "Editing"),     // dropped
            (4, "Storyteller", null, "Story", "Sound"),   // kept via job
            (5, "Producer", null, "Producer", "Production"), // dropped
        };

        var result = CollectionInsights.Compute([IM(Mv(1), crew: crew)]);

        var ids = result.TopCrew.Select(c => c.Id).ToHashSet();
        Assert.Equal(3, result.UniqueCrew);
        Assert.Equal(new HashSet<int> { 1, 2, 4 }, ids);
    }

    [Fact]
    public void Crew_credits_accumulate_across_movies()
    {
        var director = (7, "Auteur", (string?)null, "Director", "Directing");
        var result = CollectionInsights.Compute(
        [
            IM(Mv(1), crew: [director]),
            IM(Mv(2), crew: [director]),
        ]);

        Assert.Single(result.TopCrew);
        Assert.Equal(7, result.TopCrew[0].Id);
        Assert.Equal(2, result.TopCrew[0].Count);
    }

    [Fact]
    public void Year_range_and_averages_ignore_missing_values()
    {
        var movies = new[]
        {
            IM(Mv(1, year: 1995, runtime: 100, vote: 7.0)),
            IM(Mv(2, year: 2005, runtime: 120, vote: 8.0)),
            IM(Mv(3, year: 2015, runtime: null, vote: null)),
        };

        var result = CollectionInsights.Compute(movies);

        Assert.Equal((1995, 2015), result.YearRange!.Value);
        Assert.Equal(110.0, result.AverageRuntime!.Value);       // nulls excluded
        Assert.Equal(7.5, result.AverageVoteAverage!.Value);
    }

    [Fact]
    public void Year_range_and_averages_are_null_when_no_movie_supplies_them()
    {
        var result = CollectionInsights.Compute([IM(Mv(1))]);

        Assert.Null(result.YearRange);
        Assert.Null(result.AverageRuntime);
        Assert.Null(result.AverageVoteAverage);
    }
}

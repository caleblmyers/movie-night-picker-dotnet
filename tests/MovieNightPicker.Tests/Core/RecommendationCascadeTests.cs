using MovieNightPicker.Core;
using MovieNightPicker.Core.Models;
using MovieNightPicker.Core.Suggestions;

namespace MovieNightPicker.Tests.Core;

public class RecommendationCascadeTests
{
    /// <summary>
    /// A fake data source: records every query it receives and answers via an
    /// injected responder, so tests can assert which strategies ran.
    /// </summary>
    private sealed class FakeMovieDataSource(Func<DiscoverParams, IReadOnlyList<Movie>> responder)
        : IMovieDataSource
    {
        public List<DiscoverParams> Queries { get; } = [];

        public Task<IReadOnlyList<Movie>> DiscoverMoviesAsync(DiscoverParams p, CancellationToken ct = default)
        {
            Queries.Add(p);
            return Task.FromResult(responder(p));
        }

        public Task<Movie?> GetMovieAsync(int id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetMovieKeywordsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }

    private static Movie M(int id) => new(id, $"Movie {id}", null, null, null, 8.0, 500, null, [28]);

    private static readonly MoviePreferences Prefs = new()
    {
        Genres = [28, 12],
        KeywordIds = [100, 200, 300],
        YearRange = (2000, 2010),
    };

    [Fact]
    public async Task First_strategy_with_results_wins_and_short_circuits()
    {
        var source = new FakeMovieDataSource(_ => [M(1)]);

        var result = await RecommendationCascade.SuggestAsync(Prefs, new HashSet<int>(), source);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
        Assert.Single(source.Queries); // strategy 1 hit -> no further queries
    }

    [Fact]
    public async Task Strategies_run_in_order_until_one_returns()
    {
        // Answer only for strategy 3: single genre, a year range, no keywords.
        var source = new FakeMovieDataSource(p =>
            p.Genres.Count == 1 && p.Keywords.Count == 0 && p.YearRange is not null
                ? [M(42)]
                : []);

        var result = await RecommendationCascade.SuggestAsync(Prefs, new HashSet<int>(), source);

        Assert.Equal(42, result!.Id);
        Assert.Equal(3, source.Queries.Count); // strategies 1, 2, 3 ran; 3 won

        // confirm the documented shape of the first three strategies
        Assert.Equal([28, 12], source.Queries[0].Genres);     // 1: top 2 genres
        Assert.Equal([100, 200, 300], source.Queries[1].Keywords); // 2: top genre + top 3 keywords
        Assert.Equal([28], source.Queries[2].Genres);         // 3: top genre + year range
    }

    [Fact]
    public async Task Excluded_movies_are_filtered_out()
    {
        var source = new FakeMovieDataSource(_ => [M(1), M(2), M(3)]);
        var exclude = new HashSet<int> { 1, 2 };

        var result = await RecommendationCascade.SuggestAsync(Prefs, exclude, source);

        Assert.Equal(3, result!.Id); // 1 and 2 filtered, 3 survives
    }

    [Fact]
    public async Task A_strategy_whose_results_are_all_excluded_falls_through()
    {
        // Strategy 1 returns only an excluded movie; strategy 2 returns a fresh one.
        var source = new FakeMovieDataSource(p =>
            p.Keywords.Count > 0 ? [M(77)] : [M(5)]);
        var exclude = new HashSet<int> { 5 };

        var result = await RecommendationCascade.SuggestAsync(Prefs, exclude, source);

        Assert.Equal(77, result!.Id);
        Assert.Equal(2, source.Queries.Count); // strategy 1 all-excluded -> strategy 2 wins
    }

    [Fact]
    public async Task Emergency_fallback_runs_when_all_five_strategies_empty()
    {
        // Only the emergency query (relaxed floor + popularity sort) returns.
        var source = new FakeMovieDataSource(p =>
            p.SortBy == "popularity.desc" && p.VoteAverageGte == 6.0 ? [M(9)] : []);

        var result = await RecommendationCascade.SuggestAsync(Prefs, new HashSet<int>(), source);

        Assert.Equal(9, result!.Id);
        Assert.Equal(6, source.Queries.Count); // 5 strategies + emergency

        var emergency = source.Queries[^1];
        Assert.Equal(6.0, emergency.VoteAverageGte);
        Assert.Equal(100, emergency.VoteCountGte);
        Assert.Equal("popularity.desc", emergency.SortBy);
    }

    [Fact]
    public async Task Returns_null_when_even_emergency_is_empty()
    {
        var source = new FakeMovieDataSource(_ => []);

        var result = await RecommendationCascade.SuggestAsync(Prefs, new HashSet<int>(), source);

        Assert.Null(result);
        Assert.Equal(6, source.Queries.Count);
    }

    [Fact]
    public async Task Quality_strategies_apply_the_standard_floor_and_sort()
    {
        var source = new FakeMovieDataSource(_ => []);

        await RecommendationCascade.SuggestAsync(Prefs, new HashSet<int>(), source);

        foreach (var q in source.Queries.Take(5))
        {
            Assert.Equal(6.5, q.VoteAverageGte);
            Assert.Equal(150, q.VoteCountGte);
            Assert.Equal("vote_average.desc", q.SortBy);
        }
    }

    [Fact]
    public async Task Injected_selector_chooses_from_the_top_candidates()
    {
        var source = new FakeMovieDataSource(_ => [M(1), M(2), M(3)]);

        var result = await RecommendationCascade.SuggestAsync(
            Prefs, new HashSet<int>(), source, selector: candidates => candidates[^1]);

        Assert.Equal(3, result!.Id); // last of the candidate pool
    }
}

using MovieNightPicker.Core;
using MovieNightPicker.Core.Constants;
using MovieNightPicker.Core.Models;
using MovieNightPicker.Core.Suggestions;

namespace MovieNightPicker.Tests.Core;

public class SuggestFlowTests
{
    /// <summary>
    /// A fake data source: records every discover query and answers via an
    /// injected responder; resolves single movies from an optional id map (used
    /// by the round-10 anchor inference).
    /// </summary>
    private sealed class FakeSource(
        Func<DiscoverParams, IReadOnlyList<Movie>> responder,
        IReadOnlyDictionary<int, Movie>? byId = null) : IMovieDataSource
    {
        private readonly IReadOnlyDictionary<int, Movie> _byId = byId ?? new Dictionary<int, Movie>();

        public List<DiscoverParams> Queries { get; } = [];

        public Task<IReadOnlyList<Movie>> DiscoverMoviesAsync(DiscoverParams p, CancellationToken ct = default)
        {
            Queries.Add(p);
            return Task.FromResult(responder(p));
        }

        public Task<Movie?> GetMovieAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(_byId.GetValueOrDefault(id));

        public Task<IReadOnlyList<int>> GetMovieKeywordsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }

    private static Movie M(int id, params int[] genres) =>
        new(id, $"Movie {id}", null, null, null, 8.0, 500, null, genres);

    [Fact]
    public async Task Returns_four_distinct_movies_one_per_slot()
    {
        // Each genre slot yields its own movie (id = genre * 10).
        var source = new FakeSource(p =>
            p.Genres.Count > 0 ? [M(p.Genres[0] * 10, p.Genres[0])] : []);

        var result = await SuggestFlow.GetRoundAsync(1, [], source);

        Assert.Equal(RoundCategory.Genre, result.Category);
        Assert.Equal("Genre", result.CategoryLabel);
        Assert.Equal([280, 180, 350, 8780], result.Movies.Select(m => m.Id));
    }

    [Fact]
    public async Task Excluded_and_selected_movies_never_appear()
    {
        // Each genre slot offers two candidates; the first is the selected pick.
        var source = new FakeSource(p =>
            p.Genres.Count > 0 ? [M(p.Genres[0] * 10, p.Genres[0]), M(p.Genres[0] * 10 + 1, p.Genres[0])] : []);

        var result = await SuggestFlow.GetRoundAsync(1, [280], source);

        var ids = result.Movies.Select(m => m.Id).ToList();
        Assert.DoesNotContain(280, ids);   // selected -> excluded
        Assert.Contains(281, ids);          // the slot's next candidate wins
    }

    [Fact]
    public async Task Injected_selector_chooses_from_a_slots_candidates()
    {
        var source = new FakeSource(p =>
            p.Genres.Count > 0 ? [M(p.Genres[0] * 10, p.Genres[0]), M(p.Genres[0] * 10 + 1, p.Genres[0])] : []);

        var result = await SuggestFlow.GetRoundAsync(1, [], source, slotSelector: c => c[^1]);

        var ids = result.Movies.Select(m => m.Id).ToList();
        Assert.Contains(281, ids);          // last candidate of slot 28
        Assert.DoesNotContain(280, ids);
    }

    [Fact]
    public async Task Slot_falls_back_by_dropping_mood_keywords()
    {
        // The mood round attaches keywords; only the keyword-free retry returns.
        var source = new FakeSource(p =>
            p.Keywords.Count == 0 && p.Genres.Count > 0 ? [M(900, 18)] : []);

        var result = await SuggestFlow.GetRoundAsync(3, [], source);

        Assert.NotEmpty(source.Queries[0].Keywords);  // first attempt carried keywords
        Assert.Empty(source.Queries[1].Keywords);     // retry dropped them
        Assert.Contains(900, result.Movies.Select(m => m.Id));
    }

    [Fact]
    public async Task Slot_falls_back_to_genre_only_at_the_desperate_floor()
    {
        // Era round: only the relaxed genre-only attempt (5.0/50) yields anything.
        var source = new FakeSource(p =>
            p.VoteAverageGte == QualityFloors.Desperate.VoteAverageGte ? [M(700, 18)] : []);

        var result = await SuggestFlow.GetRoundAsync(2, [], source);

        Assert.Contains(700, result.Movies.Select(m => m.Id));
        Assert.Contains(source.Queries, q =>
            q.VoteAverageGte == QualityFloors.Desperate.VoteAverageGte
            && q.VoteCountGte == QualityFloors.Desperate.VoteCountGte);
    }

    [Fact]
    public async Task Dedups_across_slots_and_fills_to_four_when_slots_collide()
    {
        // Every slot query returns the same movie; only the popularity-sorted fill
        // query offers fresh ones.
        var source = new FakeSource(p =>
            p.SortBy == "popularity.desc"
                ? [M(1, 28), M(2, 28), M(3, 28), M(4, 28), M(5, 28)]
                : [M(99, 28)]);

        var result = await SuggestFlow.GetRoundAsync(1, [], source);

        var ids = result.Movies.Select(m => m.Id).ToList();
        Assert.Equal(4, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());  // no dupes
        Assert.Contains(99, ids);                          // the single slot pick
        Assert.Equal(3, ids.Count(id => id is >= 1 and <= 5)); // topped up from fill
    }

    [Fact]
    public async Task Round_returns_fewer_than_four_when_nothing_is_available()
    {
        var source = new FakeSource(_ => []);

        var result = await SuggestFlow.GetRoundAsync(1, [], source);

        Assert.Empty(result.Movies);
    }

    [Fact]
    public async Task Round_ten_anchors_on_the_most_frequent_selected_genre()
    {
        // Genre 28 appears in all three picks; 18 only once -> anchor is 28.
        var byId = new Dictionary<int, Movie>
        {
            [1] = M(1, 28),
            [2] = M(2, 28, 18),
            [3] = M(3, 28),
        };
        var source = new FakeSource(_ => [], byId);

        var result = await SuggestFlow.GetRoundAsync(10, [1, 2, 3], source);

        Assert.Equal(RoundCategory.Mixed, result.Category);
        Assert.NotEmpty(source.Queries);
        Assert.All(source.Queries, q => Assert.Equal([28], q.Genres));
    }

    [Fact]
    public async Task Round_ten_falls_back_to_drama_when_no_genres_resolve()
    {
        // Selected ids resolve to nothing -> anchor defaults to Drama (18).
        var source = new FakeSource(_ => []);

        var result = await SuggestFlow.GetRoundAsync(10, [1, 2, 3], source);

        Assert.Equal(RoundCategory.Mixed, result.Category);
        Assert.All(source.Queries, q => Assert.Equal([18], q.Genres));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Out_of_range_rounds_throw(int round)
    {
        var source = new FakeSource(_ => []);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => SuggestFlow.GetRoundAsync(round, [], source));
    }
}

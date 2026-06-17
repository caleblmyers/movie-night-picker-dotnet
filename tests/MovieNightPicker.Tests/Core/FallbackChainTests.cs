using MovieNightPicker.Core.Discovery;
using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Tests.Core;

public class FallbackChainTests
{
    [Fact]
    public void ShouldTryFallback_false_for_simple_single_genre()
    {
        Assert.False(FallbackChain.ShouldTryFallback(new DiscoverFilters { Genres = [28] }));
        Assert.False(FallbackChain.ShouldTryFallback(new DiscoverFilters()));
    }

    [Theory]
    [InlineData(new[] { 28, 12 }, new int[0], new int[0])] // multiple genres
    [InlineData(new[] { 28 }, new[] { 100 }, new int[0])]   // any actors
    [InlineData(new[] { 28 }, new int[0], new[] { 300 })]   // any crew
    public void ShouldTryFallback_true_when_warranted(int[] genres, int[] cast, int[] crew)
    {
        var filters = new DiscoverFilters { Genres = genres, Cast = cast, Crew = crew };

        Assert.True(FallbackChain.ShouldTryFallback(filters));
    }

    [Fact]
    public void No_fallback_returns_single_full_param_set()
    {
        var filters = new DiscoverFilters { Genres = [28], Era = "90s" };

        var chain = FallbackChain.Build(filters);

        var only = Assert.Single(chain);
        Assert.Equal([28], only.Genres);
        Assert.Equal((1990, 1999), only.YearRange);
    }

    [Fact]
    public void Build_produces_documented_progressive_relaxation()
    {
        var filters = new DiscoverFilters
        {
            Genres = [28, 12, 16],
            Cast = [100],
            Crew = [300],
            Mood = "dark",
            Era = "90s",
        };

        var chain = FallbackChain.Build(filters);

        // 1 full, 2 drop-mood, 3 drop-crew, 4 drop-actors, 5 single-genre,
        // 6 drop-era (7 "genre only" dedups against 6), 8 year-range-only.
        Assert.Equal(7, chain.Count);

        // (1) full: everything present, including mood-expanded keywords
        Assert.Equal([28, 12, 16], chain[0].Genres);
        Assert.Equal([100], chain[0].Actors);
        Assert.Equal([300], chain[0].Crew);
        Assert.Equal((1990, 1999), chain[0].YearRange);
        Assert.Equal([9715, 207317], chain[0].Keywords);

        // (2) mood dropped -> keywords gone, crew still present
        Assert.Empty(chain[1].Keywords);
        Assert.Equal([300], chain[1].Crew);

        // (3) crew dropped, actors remain
        Assert.Empty(chain[2].Crew);
        Assert.Equal([100], chain[2].Actors);

        // (4) actors dropped, all three genres remain
        Assert.Empty(chain[3].Actors);
        Assert.Equal([28, 12, 16], chain[3].Genres);

        // (5) reduced to a single genre
        Assert.Equal([28], chain[4].Genres);
        Assert.Equal((1990, 1999), chain[4].YearRange);

        // (6) era-derived year range dropped
        Assert.Equal([28], chain[5].Genres);
        Assert.Null(chain[5].YearRange);

        // (8) year-range only -> no genres, era range back
        Assert.Empty(chain[^1].Genres);
        Assert.Equal((1990, 1999), chain[^1].YearRange);
    }

    [Fact]
    public void Strict_filters_are_never_relaxed_across_the_chain()
    {
        var filters = new DiscoverFilters
        {
            Genres = [28, 12],
            Cast = [100],
            Mood = "dark",
            Era = "90s",
            YearRange = (2000, 2010),   // explicit -> strict
            RuntimeRange = (90, 120),    // strict
            WatchProviders = "8",        // strict
            ExcludeGenres = [27],        // strict
            ExcludeCast = [1],           // strict
            ExcludeCrew = [2],           // strict
            OriginCountries = ["US"],    // strict
            Keywords = [42],             // explicit -> strict
        };

        var chain = FallbackChain.Build(filters);

        Assert.NotEmpty(chain);
        foreach (var p in chain)
        {
            Assert.Equal((2000, 2010), p.YearRange);      // explicit range survives every step
            Assert.Equal((90, 120), p.RuntimeRange);
            Assert.Equal("8", p.WatchProviders);
            Assert.Equal([27], p.ExcludeGenres);
            Assert.Equal([1], p.ExcludeCast);
            Assert.Equal([2], p.ExcludeCrew);
            Assert.Equal(["US"], p.OriginCountries);
            Assert.Contains(42, p.Keywords);              // explicit keyword always present
        }
    }

    [Fact]
    public void Consecutive_identical_param_sets_are_deduped()
    {
        // Two genres, nothing else soft to drop: most relaxation steps coincide.
        var filters = new DiscoverFilters { Genres = [28, 12] };

        var chain = FallbackChain.Build(filters);

        // distinct sets: [28,12] -> [28] -> [] (genres only)
        Assert.Equal(3, chain.Count);
        Assert.Equal([28, 12], chain[0].Genres);
        Assert.Equal([28], chain[1].Genres);
        Assert.Empty(chain[2].Genres);

        // no two adjacent entries are equal
        for (var i = 1; i < chain.Count; i++)
        {
            Assert.NotEqual(chain[i - 1], chain[i]);
        }
    }
}

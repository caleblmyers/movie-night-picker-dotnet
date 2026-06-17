using MovieNightPicker.Core.Constants;
using MovieNightPicker.Core.Discovery;
using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Tests.Core;

public class DiscoverParamsBuilderTests
{
    [Fact]
    public void Genres_cast_crew_pass_through_to_their_targets()
    {
        var filters = new DiscoverFilters
        {
            Genres = [28, 12],
            Cast = [100, 200],
            Crew = [300],
        };

        var p = DiscoverParamsBuilder.Build(filters);

        Assert.Equal([28, 12], p.Genres);
        Assert.Equal([100, 200], p.Actors); // Cast -> Actors
        Assert.Equal([300], p.Crew);
    }

    [Fact]
    public void Era_resolves_to_year_range()
    {
        var p = DiscoverParamsBuilder.Build(new DiscoverFilters { Era = "90s" });

        Assert.Equal((1990, 1999), p.YearRange);
    }

    [Fact]
    public void Explicit_year_range_wins_over_era()
    {
        var filters = new DiscoverFilters { Era = "90s", YearRange = (2001, 2005) };

        var p = DiscoverParamsBuilder.Build(filters);

        Assert.Equal((2001, 2005), p.YearRange);
    }

    [Fact]
    public void Mood_keywords_merge_with_explicit_keywords_deduped()
    {
        // "dark" expands to 9715, 207317; one of them is also given explicitly.
        var filters = new DiscoverFilters
        {
            Keywords = [9715, 555],
            Mood = "dark",
        };

        var p = DiscoverParamsBuilder.Build(filters);

        Assert.Equal([9715, 555, 207317], p.Keywords);
    }

    [Fact]
    public void No_mood_keeps_only_explicit_keywords()
    {
        var p = DiscoverParamsBuilder.Build(new DiscoverFilters { Keywords = [1, 2] });

        Assert.Equal([1, 2], p.Keywords);
    }

    [Fact]
    public void Popularity_level_resolves_to_range()
    {
        var p = DiscoverParamsBuilder.Build(new DiscoverFilters { PopularityLevel = "HIGH" });

        Assert.Equal(PopularityLevels.High, p.PopularityRange);
    }

    [Fact]
    public void Explicit_popularity_range_wins_over_level()
    {
        var filters = new DiscoverFilters { PopularityLevel = "HIGH", PopularityRange = (5, 15) };

        var p = DiscoverParamsBuilder.Build(filters);

        Assert.Equal((5, 15), p.PopularityRange);
    }

    [Fact]
    public void Passthrough_filters_are_preserved()
    {
        var filters = new DiscoverFilters
        {
            RuntimeRange = (90, 120),
            WatchProviders = "8",
            ExcludeGenres = [27],
            ExcludeCast = [1],
            ExcludeCrew = [2],
            OriginCountries = ["US", "GB"],
        };

        var p = DiscoverParamsBuilder.Build(filters);

        Assert.Equal((90, 120), p.RuntimeRange);
        Assert.Equal("8", p.WatchProviders);
        Assert.Equal([27], p.ExcludeGenres);
        Assert.Equal([1], p.ExcludeCast);
        Assert.Equal([2], p.ExcludeCrew);
        Assert.Equal(["US", "GB"], p.OriginCountries);
    }

    [Fact]
    public void Empty_filters_produce_empty_resolved_params()
    {
        var p = DiscoverParamsBuilder.Build(new DiscoverFilters());

        Assert.Empty(p.Genres);
        Assert.Empty(p.Keywords);
        Assert.Null(p.YearRange);
        Assert.Null(p.PopularityRange);
        Assert.Null(p.RuntimeRange);
    }
}

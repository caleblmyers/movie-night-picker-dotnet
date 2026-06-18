using System.Web;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Internal;

namespace MovieNightPicker.Tests.Tmdb;

public class TmdbQueryStringBuilderTests
{
    // Parses the builder's output back into a key->value map so assertions can ignore
    // ordering and inspect decoded values.
    private static Dictionary<string, string> Parse(string query)
    {
        var parsed = HttpUtility.ParseQueryString(query.TrimStart('?'));
        return parsed.AllKeys
            .Where(k => k is not null)
            .ToDictionary(k => k!, k => parsed[k]!);
    }

    [Fact]
    public void ForDiscover_JoinsIdListsWithCommas()
    {
        var p = new DiscoverParams
        {
            Genres = [28, 12, 16],
            Actors = [500, 287],
            Crew = [525],
            Keywords = [9715, 207317],
        };

        var q = Parse(TmdbQueryStringBuilder.ToQueryString(TmdbQueryStringBuilder.ForDiscover(p)));

        Assert.Equal("28,12,16", q["with_genres"]);
        Assert.Equal("500,287", q["with_cast"]);
        Assert.Equal("525", q["with_crew"]);
        Assert.Equal("9715,207317", q["with_keywords"]);
    }

    [Fact]
    public void ForDiscover_MapsYearRangeToPrimaryReleaseDateBounds()
    {
        var p = new DiscoverParams { YearRange = (2000, 2020) };

        var q = Parse(TmdbQueryStringBuilder.ToQueryString(TmdbQueryStringBuilder.ForDiscover(p)));

        Assert.Equal("2000-01-01", q["primary_release_date.gte"]);
        Assert.Equal("2020-12-31", q["primary_release_date.lte"]);
    }

    [Fact]
    public void ForDiscover_MapsExcludeFilters()
    {
        var p = new DiscoverParams
        {
            ExcludeGenres = [27, 53],
            ExcludeCast = [100],
            ExcludeCrew = [200, 201],
        };

        var q = Parse(TmdbQueryStringBuilder.ToQueryString(TmdbQueryStringBuilder.ForDiscover(p)));

        Assert.Equal("27,53", q["without_genres"]);
        Assert.Equal("100", q["without_cast"]);
        Assert.Equal("200,201", q["without_crew"]);
    }

    [Fact]
    public void ForDiscover_MapsRuntimePopularityVotesAndProviders()
    {
        var p = new DiscoverParams
        {
            RuntimeRange = (90, 150),
            PopularityRange = (20, 100),
            VoteAverageGte = 6.5,
            VoteCountGte = 150,
            WatchProviders = "8",
            OriginCountries = ["US", "GB"],
        };

        var q = Parse(TmdbQueryStringBuilder.ToQueryString(TmdbQueryStringBuilder.ForDiscover(p)));

        Assert.Equal("90", q["with_runtime.gte"]);
        Assert.Equal("150", q["with_runtime.lte"]);
        Assert.Equal("20", q["popularity.gte"]);
        Assert.Equal("100", q["popularity.lte"]);
        Assert.Equal("6.5", q["vote_average.gte"]);
        Assert.Equal("150", q["vote_count.gte"]);
        Assert.Equal("8", q["with_watch_providers"]);
        Assert.Equal("US,GB", q["with_origin_country"]);
    }

    [Fact]
    public void ForDiscover_EmptyParams_ProducesNoKeys()
    {
        var q = TmdbQueryStringBuilder.ToQueryString(TmdbQueryStringBuilder.ForDiscover(new DiscoverParams()));

        Assert.Equal(string.Empty, q);
    }

    [Theory]
    [InlineData(null)]
    public void ForDiscover_NullOptionalRanges_OmitTheirKeys(int? _)
    {
        var p = new DiscoverParams { Genres = [28] };

        var q = Parse(TmdbQueryStringBuilder.ToQueryString(TmdbQueryStringBuilder.ForDiscover(p)));

        Assert.False(q.ContainsKey("primary_release_date.gte"));
        Assert.False(q.ContainsKey("with_runtime.gte"));
        Assert.False(q.ContainsKey("popularity.gte"));
        Assert.False(q.ContainsKey("vote_average.gte"));
        Assert.True(q.ContainsKey("with_genres"));
    }

    [Fact]
    public void BuildDiscoverQuery_AlwaysAppendsApiKeyAndStartsWithQuestionMark()
    {
        var query = TmdbQueryStringBuilder.BuildDiscoverQuery(
            new DiscoverParams { Genres = [28] },
            options: null,
            apiKey: "secret-key");

        Assert.StartsWith("?", query);
        var q = Parse(query);
        Assert.Equal("secret-key", q["api_key"]);
        Assert.Equal("28", q["with_genres"]);
    }

    [Fact]
    public void BuildDiscoverQuery_IncludesRequestOptionDefaults()
    {
        var query = TmdbQueryStringBuilder.BuildDiscoverQuery(
            new DiscoverParams(),
            new TmdbRequestOptions(),
            apiKey: "k");

        var q = Parse(query);

        Assert.Equal("en-US", q["language"]);
        Assert.Equal("US", q["region"]);
        Assert.Equal("popularity.desc", q["sort_by"]);
        Assert.Equal("1", q["page"]);
        Assert.Equal("false", q["include_adult"]);
    }

    [Fact]
    public void BuildDiscoverQuery_DiscoverParamsWinOverOptions_NoDuplicateKeys()
    {
        // Both sides set the same overlapping keys; discover should win and each key
        // must appear exactly once.
        var query = TmdbQueryStringBuilder.BuildDiscoverQuery(
            new DiscoverParams
            {
                VoteAverageGte = 7.5,
                VoteCountGte = 500,
                PopularityRange = (10, 90),
                WatchProviders = "8",
            },
            new TmdbRequestOptions
            {
                VoteAverageGte = 1.0,
                VoteCountGte = 1,
                PopularityGte = 1,
                PopularityLte = 5,
                WithWatchProviders = "337",
            },
            apiKey: "k");

        // Raw key occurrences (not the de-duped Parse map) prove no key is emitted twice.
        var keys = query.TrimStart('?').Split('&').Select(p => p.Split('=')[0]).ToList();
        foreach (var key in new[] { "vote_average.gte", "vote_count.gte", "popularity.gte", "popularity.lte", "with_watch_providers" })
        {
            Assert.Equal(1, keys.Count(k => k == key));
        }

        var q = Parse(query);
        Assert.Equal("7.5", q["vote_average.gte"]);
        Assert.Equal("500", q["vote_count.gte"]);
        Assert.Equal("10", q["popularity.gte"]);
        Assert.Equal("90", q["popularity.lte"]);
        Assert.Equal("8", q["with_watch_providers"]);
    }

    [Fact]
    public void BuildDiscoverQuery_UnsetDiscoverKey_FallsBackToOptionValue()
    {
        // Discover leaves vote_average.gte unset, so the option's value should be used.
        var query = TmdbQueryStringBuilder.BuildDiscoverQuery(
            new DiscoverParams { Genres = [28] },
            new TmdbRequestOptions { VoteAverageGte = 6.5 },
            apiKey: "k");

        var keys = query.TrimStart('?').Split('&').Select(p => p.Split('=')[0]).ToList();
        Assert.Equal(1, keys.Count(k => k == "vote_average.gte"));

        var q = Parse(query);
        Assert.Equal("6.5", q["vote_average.gte"]);
    }

    [Fact]
    public void ToQueryString_EscapesValues()
    {
        var pairs = new[] { new KeyValuePair<string, string?>("query", "the dark knight & robin") };

        var query = TmdbQueryStringBuilder.ToQueryString(pairs);

        Assert.Contains("query=the%20dark%20knight%20%26%20robin", query);
    }

    [Fact]
    public void BuildOptionsQuery_MergesExtrasAndApiKey()
    {
        var query = TmdbQueryStringBuilder.BuildOptionsQuery(
            new TmdbRequestOptions(),
            "k",
            [new KeyValuePair<string, string?>("query", "inception")]);

        var q = Parse(query);

        Assert.Equal("inception", q["query"]);
        Assert.Equal("k", q["api_key"]);
        Assert.Equal("en-US", q["language"]);
    }
}

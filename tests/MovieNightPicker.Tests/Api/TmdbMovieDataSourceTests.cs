using MovieNightPicker.Api.Adapters;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Dtos;
using CoreModels = MovieNightPicker.Core.Models;
using TmdbDiscoverParams = MovieNightPicker.Tmdb.DiscoverParams;

namespace MovieNightPicker.Tests.Api;

public class TmdbMovieDataSourceTests
{
    /// <summary>
    /// A hand-rolled fake TMDB client (no Moq). Each delegate defaults to throwing
    /// so a test only wires up the call it exercises.
    /// </summary>
    private sealed class FakeTmdbClient : ITmdbClient
    {
        public Func<TmdbDiscoverParams, TmdbRequestOptions?, TmdbPagedResult<TmdbMovie>>? OnDiscover { get; set; }
        public Func<int, TmdbMovie>? OnGetMovie { get; set; }
        public Func<int, IReadOnlyList<TmdbKeyword>>? OnGetKeywords { get; set; }

        public TmdbDiscoverParams? LastDiscover { get; private set; }
        public TmdbRequestOptions? LastOptions { get; private set; }

        public Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(
            TmdbDiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default)
        {
            LastDiscover = discover;
            LastOptions = options;
            return Task.FromResult(OnDiscover!(discover, options));
        }

        public Task<TmdbMovie> GetMovieAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(OnGetMovie!(id));

        public Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(OnGetKeywords!(id));

        public Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(
            string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbCredits> GetMovieCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPerson> GetPersonAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbCredits> GetPersonCombinedCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static TmdbPagedResult<TmdbMovie> Page(params TmdbMovie[] movies) =>
        new() { Page = 1, Results = movies, TotalPages = 1, TotalResults = movies.Length };

    [Fact]
    public async Task DiscoverMoviesAsync_maps_dto_fields_to_domain()
    {
        var client = new FakeTmdbClient
        {
            OnDiscover = (_, _) => Page(new TmdbMovie
            {
                Id = 603,
                Title = "The Matrix",
                Overview = "A hacker learns the truth.",
                PosterPath = "/matrix.jpg",
                ReleaseDate = "1999-03-30",
                VoteAverage = 8.2,
                VoteCount = 24000,
                Runtime = 136,
                Genres = [new TmdbGenre { Id = 28 }, new TmdbGenre { Id = 878 }],
            }),
        };
        var source = new TmdbMovieDataSource(client);

        var result = await source.DiscoverMoviesAsync(new CoreModels.DiscoverParams { Genres = [28] });

        var movie = Assert.Single(result);
        Assert.Equal(603, movie.Id);
        Assert.Equal("The Matrix", movie.Title);
        Assert.Equal("A hacker learns the truth.", movie.Overview);
        Assert.Equal("/matrix.jpg", movie.PosterPath);
        Assert.Equal(new DateOnly(1999, 3, 30), movie.ReleaseDate);
        Assert.Equal(8.2, movie.VoteAverage);
        Assert.Equal(24000, movie.VoteCount);
        Assert.Equal(136, movie.Runtime);
        Assert.Equal([28, 878], movie.Genres);
    }

    [Fact]
    public async Task DiscoverMoviesAsync_maps_core_params_onto_tmdb_params_and_options()
    {
        var client = new FakeTmdbClient { OnDiscover = (_, _) => Page() };
        var source = new TmdbMovieDataSource(client);

        await source.DiscoverMoviesAsync(new CoreModels.DiscoverParams
        {
            Genres = [28, 12],
            YearRange = (2000, 2010),
            Keywords = [100],
            PopularityRange = (10, 90),
            VoteAverageGte = 6.5,
            VoteCountGte = 300,
            SortBy = "vote_average.desc",
            Page = 3,
        });

        Assert.Equal([28, 12], client.LastDiscover!.Genres);
        Assert.Equal((2000, 2010), client.LastDiscover.YearRange);
        Assert.Equal([100], client.LastDiscover.Keywords);
        Assert.Equal((10d, 90d), client.LastDiscover.PopularityRange);
        Assert.Equal(6.5, client.LastDiscover.VoteAverageGte);
        Assert.Equal(300, client.LastDiscover.VoteCountGte);
        Assert.Equal("vote_average.desc", client.LastOptions!.SortBy);
        Assert.Equal(3, client.LastOptions.Page);
    }

    [Fact]
    public async Task DiscoverMoviesAsync_handles_null_fields()
    {
        var client = new FakeTmdbClient
        {
            OnDiscover = (_, _) => Page(new TmdbMovie { Id = 1 }), // Title/dates/etc all null
        };
        var source = new TmdbMovieDataSource(client);

        var movie = Assert.Single(await source.DiscoverMoviesAsync(new CoreModels.DiscoverParams()));
        Assert.Equal(1, movie.Id);
        Assert.Equal(string.Empty, movie.Title);
        Assert.Null(movie.Overview);
        Assert.Null(movie.ReleaseDate);
        Assert.Null(movie.Runtime);
        Assert.Empty(movie.Genres);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public async Task DiscoverMoviesAsync_treats_unparseable_release_date_as_null(string? raw)
    {
        var client = new FakeTmdbClient
        {
            OnDiscover = (_, _) => Page(new TmdbMovie { Id = 1, ReleaseDate = raw }),
        };
        var source = new TmdbMovieDataSource(client);

        var movie = Assert.Single(await source.DiscoverMoviesAsync(new CoreModels.DiscoverParams()));
        Assert.Null(movie.ReleaseDate);
    }

    [Fact]
    public async Task GetMovieAsync_maps_when_found()
    {
        var client = new FakeTmdbClient
        {
            OnGetMovie = id => new TmdbMovie { Id = id, Title = "Found", ReleaseDate = "2010-07-16" },
        };
        var source = new TmdbMovieDataSource(client);

        var movie = await source.GetMovieAsync(27205);
        Assert.NotNull(movie);
        Assert.Equal(27205, movie!.Id);
        Assert.Equal("Found", movie.Title);
        Assert.Equal(new DateOnly(2010, 7, 16), movie.ReleaseDate);
    }

    [Fact]
    public async Task GetMovieAsync_returns_null_on_404()
    {
        var client = new FakeTmdbClient
        {
            OnGetMovie = _ => throw new TmdbApiException("TMDB API error: 404 - Not Found", 404),
        };
        var source = new TmdbMovieDataSource(client);

        Assert.Null(await source.GetMovieAsync(999999));
    }

    [Fact]
    public async Task GetMovieAsync_rethrows_non_404_errors()
    {
        var client = new FakeTmdbClient
        {
            OnGetMovie = _ => throw new TmdbApiException("TMDB API error: 500 - Server Error", 500),
        };
        var source = new TmdbMovieDataSource(client);

        await Assert.ThrowsAsync<TmdbApiException>(() => source.GetMovieAsync(1));
    }

    [Fact]
    public async Task GetMovieKeywordsAsync_projects_keyword_ids()
    {
        var client = new FakeTmdbClient
        {
            OnGetKeywords = _ =>
            [
                new TmdbKeyword { Id = 9715, Name = "superhero" },
                new TmdbKeyword { Id = 4565, Name = "dystopia" },
            ],
        };
        var source = new TmdbMovieDataSource(client);

        Assert.Equal([9715, 4565], await source.GetMovieKeywordsAsync(1));
    }
}

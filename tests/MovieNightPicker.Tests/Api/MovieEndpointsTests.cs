using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MovieNightPicker.Core;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Dtos;
using CoreModels = MovieNightPicker.Core.Models;
using TmdbDiscoverParams = MovieNightPicker.Tmdb.DiscoverParams;

namespace MovieNightPicker.Tests.Api;

public class MovieEndpointsTests
{
    /// <summary>A configurable fake TMDB client — only the calls a test needs are wired.</summary>
    private sealed class FakeTmdbClient : ITmdbClient
    {
        public Func<string, TmdbPagedResult<TmdbMovie>>? OnSearchMovies { get; set; }
        public Func<string, TmdbPagedResult<TmdbPerson>>? OnSearchPeople { get; set; }
        public Func<int, TmdbMovie>? OnGetMovie { get; set; }
        public Func<int, TmdbPerson>? OnGetPerson { get; set; }
        public Func<int, TmdbCredits>? OnGetCredits { get; set; }

        public Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(OnSearchMovies!(query));

        public Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(OnSearchPeople!(query));

        public Task<TmdbMovie> GetMovieAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(OnGetMovie!(id));

        public Task<TmdbPerson> GetPersonAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(OnGetPerson!(id));

        public Task<TmdbCredits> GetMovieCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(OnGetCredits is null ? new TmdbCredits() : OnGetCredits(id));

        public Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(TmdbDiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TmdbKeyword>>([]);

        public Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbCredits> GetPersonCombinedCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>A fake Core data source returning a fixed candidate pool.</summary>
    private sealed class FakeMovieDataSource : IMovieDataSource
    {
        public Func<CoreModels.DiscoverParams, IReadOnlyList<CoreModels.Movie>> OnDiscover { get; set; } = _ => [];
        public Func<int, CoreModels.Movie?> OnGetMovie { get; set; } = _ => null;

        public Task<IReadOnlyList<CoreModels.Movie>> DiscoverMoviesAsync(CoreModels.DiscoverParams p, CancellationToken ct = default) =>
            Task.FromResult(OnDiscover(p));

        public Task<CoreModels.Movie?> GetMovieAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(OnGetMovie(id));

        public Task<IReadOnlyList<int>> GetMovieKeywordsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }

    private static CoreModels.Movie Movie(int id) =>
        new(id, $"Movie {id}", null, null, new DateOnly(2005, 1, 1), 8.0, 500, 120, [28]);

    /// <summary>Spin up the API with the supplied fakes swapped in for the real services.</summary>
    private static WebApplicationFactory<Program> Factory(FakeTmdbClient tmdb, FakeMovieDataSource source) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.AddSingleton<ITmdbClient>(tmdb);
                services.AddSingleton<IMovieDataSource>(source);
            }));

    [Fact]
    public async Task Search_returns_200_with_mapped_results()
    {
        var tmdb = new FakeTmdbClient
        {
            OnSearchMovies = _ => new TmdbPagedResult<TmdbMovie>
            {
                Page = 1,
                TotalPages = 1,
                TotalResults = 1,
                Results = [new TmdbMovie { Id = 603, Title = "The Matrix" }],
            },
        };
        using var factory = Factory(tmdb, new FakeMovieDataSource());
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/movies/search?query=matrix");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<MoviePageDto>();
        Assert.NotNull(page);
        var movie = Assert.Single(page!.Results);
        Assert.Equal(603, movie.Id);
        Assert.Equal("The Matrix", movie.Title);
    }

    [Fact]
    public async Task Search_without_query_returns_400()
    {
        using var factory = Factory(new FakeTmdbClient(), new FakeMovieDataSource());
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/movies/search?query=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Discover_returns_first_non_empty_page()
    {
        var source = new FakeMovieDataSource { OnDiscover = _ => [Movie(1), Movie(2)] };
        using var factory = Factory(new FakeTmdbClient(), source);
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/movies/discover?genres=28");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var movies = await response.Content.ReadFromJsonAsync<List<MovieDto>>();
        Assert.NotNull(movies);
        Assert.Equal([1, 2], movies!.Select(m => m.Id));
    }

    [Fact]
    public async Task Detail_returns_movie_then_404()
    {
        var tmdb = new FakeTmdbClient
        {
            OnGetMovie = id => id == 1
                ? new TmdbMovie { Id = 1, Title = "Found" }
                : throw new TmdbApiException("TMDB API error: 404 - Not Found", 404),
        };
        using var factory = Factory(tmdb, new FakeMovieDataSource());
        using var http = factory.CreateClient();

        var ok = await http.GetAsync("/movies/1");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var movie = await ok.Content.ReadFromJsonAsync<MovieDto>();
        Assert.Equal("Found", movie!.Title);

        var missing = await http.GetAsync("/movies/999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Suggest_excludes_selected_ids_and_returns_a_pick()
    {
        // The candidate pool includes the two selected ids plus a fresh one (99).
        var source = new FakeMovieDataSource
        {
            OnGetMovie = id => Movie(id),
            OnDiscover = _ => [Movie(1), Movie(2), Movie(99)],
        };
        var tmdb = new FakeTmdbClient
        {
            OnGetCredits = _ => new TmdbCredits
            {
                Cast = [new TmdbCastMember { Id = 500, Name = "Actor" }],
                Crew = [new TmdbCrewMember { Id = 600, Name = "Dir", Job = "Director" }],
            },
        };
        using var factory = Factory(tmdb, source);
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/movies/suggest", new { selectedMovieIds = new[] { 1, 2 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suggestion = await response.Content.ReadFromJsonAsync<MovieDto>();
        Assert.NotNull(suggestion);
        Assert.Equal(99, suggestion!.Id);
        Assert.DoesNotContain(suggestion.Id, new[] { 1, 2 });
    }

    [Fact]
    public async Task Suggest_with_empty_body_returns_400()
    {
        using var factory = Factory(new FakeTmdbClient(), new FakeMovieDataSource());
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/movies/suggest", new { selectedMovieIds = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task People_search_and_detail_work()
    {
        var tmdb = new FakeTmdbClient
        {
            OnSearchPeople = _ => new TmdbPagedResult<TmdbPerson>
            {
                Page = 1,
                TotalPages = 1,
                TotalResults = 1,
                Results = [new TmdbPerson { Id = 6384, Name = "Keanu Reeves" }],
            },
            OnGetPerson = id => new TmdbPerson { Id = id, Name = "Keanu Reeves" },
        };
        using var factory = Factory(tmdb, new FakeMovieDataSource());
        using var http = factory.CreateClient();

        var search = await http.GetAsync("/people/search?query=keanu");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var people = await search.Content.ReadFromJsonAsync<PersonPageDto>();
        Assert.Equal("Keanu Reeves", Assert.Single(people!.Results).Name);

        var detail = await http.GetAsync("/people/6384");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var person = await detail.Content.ReadFromJsonAsync<PersonDto>();
        Assert.Equal(6384, person!.Id);
    }

    // Lightweight DTOs for deserializing responses (mirrors the API contracts).
    private sealed record MovieDto(int Id, string Title);
    private sealed record MoviePageDto(int Page, int TotalPages, int TotalResults, List<MovieDto> Results);
    private sealed record PersonDto(int Id, string Name);
    private sealed record PersonPageDto(int Page, int TotalPages, int TotalResults, List<PersonDto> Results);
}

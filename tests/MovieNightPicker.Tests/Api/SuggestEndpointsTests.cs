using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Endpoints;
using MovieNightPicker.Core;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Dtos;
using CoreModels = MovieNightPicker.Core.Models;
using TmdbDiscoverParams = MovieNightPicker.Tmdb.DiscoverParams;

namespace MovieNightPicker.Tests.Api;

public class SuggestEndpointsTests
{
    private static CoreModels.Movie Movie(int id, params int[] genres) =>
        new(id, $"Movie {id}", null, null, new DateOnly(2005, 1, 1), 8.0, 500, 120, genres.Length > 0 ? genres : [28]);

    /// <summary>
    /// A fake Core data source that records every call (so tests can assert which
    /// picks were enriched) and answers discover/get/keyword queries via injectable
    /// responders. Call records use thread-safe bags — enrichment runs in parallel.
    /// </summary>
    private sealed class FakeMovieDataSource : IMovieDataSource
    {
        public Func<CoreModels.DiscoverParams, IReadOnlyList<CoreModels.Movie>> OnDiscover { get; set; } = _ => [];
        public Func<int, CoreModels.Movie?> OnGetMovie { get; set; } = _ => null;
        public Func<int, IReadOnlyList<int>> OnGetKeywords { get; set; } = _ => [];

        public ConcurrentBag<int> GetMovieCalls { get; } = [];
        public ConcurrentBag<int> KeywordCalls { get; } = [];

        public Task<IReadOnlyList<CoreModels.Movie>> DiscoverMoviesAsync(CoreModels.DiscoverParams p, CancellationToken ct = default) =>
            Task.FromResult(OnDiscover(p));

        public Task<CoreModels.Movie?> GetMovieAsync(int id, CancellationToken ct = default)
        {
            GetMovieCalls.Add(id);
            return Task.FromResult(OnGetMovie(id));
        }

        public Task<IReadOnlyList<int>> GetMovieKeywordsAsync(int id, CancellationToken ct = default)
        {
            KeywordCalls.Add(id);
            return Task.FromResult(OnGetKeywords(id));
        }
    }

    /// <summary>A fake TMDB client that records credit lookups and returns canned credits.</summary>
    private sealed class RecordingTmdbClient : ITmdbClient
    {
        public ConcurrentBag<int> CreditCalls { get; } = [];

        public Task<TmdbCredits> GetMovieCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default)
        {
            CreditCalls.Add(id);
            return Task.FromResult(new TmdbCredits
            {
                Cast = [new TmdbCastMember { Id = 500, Name = "Actor" }],
                Crew = [new TmdbCrewMember { Id = 600, Name = "Dir", Job = "Director" }],
            });
        }

        public Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbMovie> GetMovieAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPerson> GetPersonAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(TmdbDiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(int id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbCredits> GetPersonCombinedCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task Round_returns_four_movies_with_category_and_label()
    {
        // Each genre slot yields its own movie (id = genre*10), mirroring SuggestFlow tests.
        var source = new FakeMovieDataSource
        {
            OnDiscover = p => p.Genres.Count > 0 ? [Movie(p.Genres[0] * 10, p.Genres[0])] : [],
        };

        var result = await SuggestEndpoints.RoundAsync(1, new SuggestRoundRequest([]), source, default);

        var ok = Assert.IsType<Ok<SuggestRoundResponse>>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal("Genre", ok.Value!.Category);
        Assert.Equal("Genre", ok.Value.CategoryLabel);
        Assert.Equal(4, ok.Value.Movies.Count);
    }

    [Fact]
    public async Task Round_excludes_already_selected_ids()
    {
        // Each genre slot offers two candidates; the first id is already selected.
        var source = new FakeMovieDataSource
        {
            OnDiscover = p => p.Genres.Count > 0
                ? [Movie(p.Genres[0] * 10, p.Genres[0]), Movie(p.Genres[0] * 10 + 1, p.Genres[0])]
                : [],
        };

        var result = await SuggestEndpoints.RoundAsync(1, new SuggestRoundRequest([280]), source, default);

        var ok = Assert.IsType<Ok<SuggestRoundResponse>>(result);
        var ids = ok.Value!.Movies.Select(m => m.Id).ToList();
        Assert.DoesNotContain(280, ids);
        Assert.Contains(281, ids);
    }

    [Fact]
    public async Task Round_tolerates_a_null_body()
    {
        var source = new FakeMovieDataSource
        {
            OnDiscover = p => p.Genres.Count > 0 ? [Movie(p.Genres[0] * 10, p.Genres[0])] : [],
        };

        var result = await SuggestEndpoints.RoundAsync(1, body: null, source, default);

        var ok = Assert.IsType<Ok<SuggestRoundResponse>>(result);
        Assert.Equal(4, ok.Value!.Movies.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Round_out_of_range_returns_400(int round)
    {
        var source = new FakeMovieDataSource();

        var result = await SuggestEndpoints.RoundAsync(round, new SuggestRoundRequest([]), source, default);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
    }

    /// <summary>Spin up the API with the supplied fakes swapped in for the real services.</summary>
    private static WebApplicationFactory<Program> Factory(ITmdbClient tmdb, IMovieDataSource source) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.AddSingleton(tmdb);
                services.AddSingleton(source);
            }));

    [Fact]
    public async Task Suggest_enriches_every_resolved_pick_and_skips_unknown_ones()
    {
        // id 2 resolves to null (unknown to TMDB) and must be skipped before its
        // credits/keywords are fetched; the parallelized path must still enrich the
        // rest and produce the same suggestion (the only fresh candidate, 99).
        var source = new FakeMovieDataSource
        {
            OnGetMovie = id => id == 2 ? null : Movie(id),
            OnDiscover = _ => [Movie(1), Movie(3), Movie(99)],
        };
        var tmdb = new RecordingTmdbClient();

        using var factory = Factory(tmdb, source);
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/movies/suggest", new { selectedMovieIds = new[] { 1, 2, 3 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suggestion = await response.Content.ReadFromJsonAsync<MovieDto>();
        Assert.Equal(99, suggestion!.Id);

        // Every pick was looked up; credits + keywords fetched only for resolved picks.
        Assert.Equal([1, 2, 3], source.GetMovieCalls.OrderBy(x => x));
        Assert.Equal([1, 3], tmdb.CreditCalls.OrderBy(x => x));
        Assert.Equal([1, 3], source.KeywordCalls.OrderBy(x => x));
    }

    private sealed record MovieDto(int Id, string Title);
}

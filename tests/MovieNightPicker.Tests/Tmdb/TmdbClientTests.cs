using System.Net;
using Microsoft.Extensions.Options;
using MovieNightPicker.Tmdb;

namespace MovieNightPicker.Tests.Tmdb;

public class TmdbClientTests
{
    private static TmdbClient CreateClient(
        HttpStatusCode status, string body, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(status, body);
        var http = new HttpClient(handler);
        var options = Options.Create(new TmdbClientOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.themoviedb.org/3",
        });
        return new TmdbClient(http, options);
    }

    private const string DiscoverJson =
        """
        {
          "page": 1,
          "results": [
            { "id": 27205, "title": "Inception", "vote_average": 8.4, "vote_count": 35000 }
          ],
          "total_pages": 1,
          "total_results": 1
        }
        """;

    [Fact]
    public async Task DiscoverMoviesAsync_DeserializesPagedResult()
    {
        var client = CreateClient(HttpStatusCode.OK, DiscoverJson, out _);

        var result = await client.DiscoverMoviesAsync(new DiscoverParams { Genres = [878] });

        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.TotalResults);
        var movie = Assert.Single(result.Results);
        Assert.Equal(27205, movie.Id);
        Assert.Equal("Inception", movie.Title);
        Assert.Equal(8.4, movie.VoteAverage);
    }

    [Fact]
    public async Task DiscoverMoviesAsync_BuildsUrlWithExpectedQueryParams()
    {
        var client = CreateClient(HttpStatusCode.OK, DiscoverJson, out var handler);

        await client.DiscoverMoviesAsync(
            new DiscoverParams { Genres = [28, 12] },
            new TmdbRequestOptions { Page = 2 });

        var uri = handler.LastRequestUri!.ToString();
        Assert.Contains("/discover/movie", uri);
        Assert.Contains("with_genres=28%2C12", uri); // comma escaped by the builder
        Assert.Contains("page=2", uri);
        Assert.Contains("api_key=test-key", uri);
    }

    [Fact]
    public async Task SearchMoviesAsync_IncludesQueryParam()
    {
        var client = CreateClient(HttpStatusCode.OK, DiscoverJson, out var handler);

        await client.SearchMoviesAsync("inception");

        var uri = handler.LastRequestUri!.ToString();
        Assert.Contains("/search/movie", uri);
        Assert.Contains("query=inception", uri);
        Assert.Contains("api_key=test-key", uri);
    }

    [Fact]
    public async Task GetGenresAsync_UnwrapsGenresEnvelope()
    {
        const string json = """{ "genres": [ { "id": 28, "name": "Action" }, { "id": 35, "name": "Comedy" } ] }""";
        var client = CreateClient(HttpStatusCode.OK, json, out _);

        var genres = await client.GetGenresAsync();

        Assert.Equal(2, genres.Count);
        Assert.Equal("Action", genres[0].Name);
    }

    [Fact]
    public async Task GetMovieKeywordsAsync_UnwrapsKeywordsEnvelope()
    {
        const string json = """{ "id": 27205, "keywords": [ { "id": 818, "name": "based on novel" } ] }""";
        var client = CreateClient(HttpStatusCode.OK, json, out _);

        var keywords = await client.GetMovieKeywordsAsync(27205);

        var keyword = Assert.Single(keywords);
        Assert.Equal(818, keyword.Id);
        Assert.Equal("based on novel", keyword.Name);
    }

    [Fact]
    public async Task NonSuccess_WithStatusMessage_ThrowsExactMessage()
    {
        const string body =
            """{ "status_code": 7, "status_message": "Invalid API key: You must be granted a valid key." }""";
        var client = CreateClient(HttpStatusCode.Unauthorized, body, out _);

        var ex = await Assert.ThrowsAsync<TmdbApiException>(() => client.GetMovieAsync(27205));

        Assert.Equal(
            "TMDB API error: 401 - Invalid API key: You must be granted a valid key.",
            ex.Message);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task NonSuccess_WithoutBody_FallsBackToReasonPhrase()
    {
        var client = CreateClient(HttpStatusCode.NotFound, string.Empty, out _);

        var ex = await Assert.ThrowsAsync<TmdbApiException>(() => client.GetMovieAsync(404));

        Assert.Equal("TMDB API error: 404 - Not Found", ex.Message);
        Assert.Equal(404, ex.StatusCode);
    }
}

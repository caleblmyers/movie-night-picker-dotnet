using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MovieNightPicker.Tmdb.Dtos;
using MovieNightPicker.Tmdb.Internal;

namespace MovieNightPicker.Tmdb;

/// <summary>
/// <see cref="ITmdbClient"/> backed by an injected <see cref="HttpClient"/>. Builds
/// request URLs with <see cref="TmdbQueryStringBuilder"/>, deserializes responses with
/// <see cref="System.Text.Json"/>, and surfaces non-success statuses as
/// <see cref="TmdbApiException"/>.
/// </summary>
public sealed class TmdbClient : ITmdbClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public TmdbClient(HttpClient http, IOptions<TmdbClientOptions> options)
    {
        _http = http;
        var opts = options.Value;
        _baseUrl = opts.BaseUrl.TrimEnd('/');
        _apiKey = opts.ApiKey;
    }

    public Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(
        string query, TmdbRequestOptions? options = null, CancellationToken ct = default)
    {
        var qs = TmdbQueryStringBuilder.BuildOptionsQuery(options, _apiKey, [Kv("query", query)]);
        return SendAsync<TmdbPagedResult<TmdbMovie>>($"/search/movie{qs}", ct);
    }

    public Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(
        DiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default)
    {
        var qs = TmdbQueryStringBuilder.BuildDiscoverQuery(discover, options, _apiKey);
        return SendAsync<TmdbPagedResult<TmdbMovie>>($"/discover/movie{qs}", ct);
    }

    public Task<TmdbMovie> GetMovieAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        SendAsync<TmdbMovie>($"/movie/{id}{SimpleQuery(options)}", ct);

    public Task<TmdbCredits> GetMovieCreditsAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        SendAsync<TmdbCredits>($"/movie/{id}/credits{SimpleQuery(options)}", ct);

    public async Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(
        int id, CancellationToken ct = default)
    {
        var response = await SendAsync<KeywordsResponse>($"/movie/{id}/keywords{SimpleQuery(null)}", ct)
            .ConfigureAwait(false);
        return response.Keywords;
    }

    public async Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(
        TmdbRequestOptions? options = null, CancellationToken ct = default)
    {
        var response = await SendAsync<GenresResponse>($"/genre/movie/list{SimpleQuery(options)}", ct)
            .ConfigureAwait(false);
        return response.Genres;
    }

    public Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(
        string query, TmdbRequestOptions? options = null, CancellationToken ct = default)
    {
        var qs = TmdbQueryStringBuilder.BuildOptionsQuery(options, _apiKey, [Kv("query", query)]);
        return SendAsync<TmdbPagedResult<TmdbPerson>>($"/search/person{qs}", ct);
    }

    public Task<TmdbPerson> GetPersonAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        SendAsync<TmdbPerson>($"/person/{id}{SimpleQuery(options)}", ct);

    public Task<TmdbCredits> GetPersonCombinedCreditsAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        SendAsync<TmdbCredits>($"/person/{id}/combined_credits{SimpleQuery(options)}", ct);

    // Detail / list endpoints only need language + api_key — sort_by/page would be noise.
    private string SimpleQuery(TmdbRequestOptions? options)
    {
        var language = (options ?? new TmdbRequestOptions()).Language;
        return TmdbQueryStringBuilder.ToQueryString([Kv("language", language), Kv("api_key", _apiKey)]);
    }

    private async Task<T> SendAsync<T>(string relativeUrl, CancellationToken ct)
    {
        using var response = await _http.GetAsync(_baseUrl + relativeUrl, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var reason = TryReadStatusMessage(body) ?? response.ReasonPhrase ?? response.StatusCode.ToString();
            throw new TmdbApiException($"TMDB API error: {(int)response.StatusCode} - {reason}", (int)response.StatusCode);
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new TmdbApiException(
                $"TMDB API error: {(int)response.StatusCode} - empty or unparseable response body",
                (int)response.StatusCode);
    }

    // TMDB error bodies look like {"status_code":7,"status_message":"..."}.
    private static string? TryReadStatusMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("status_message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body — fall back to the HTTP reason phrase.
        }

        return null;
    }

    private static KeyValuePair<string, string?> Kv(string key, string? value) => new(key, value);

    private sealed record KeywordsResponse
    {
        [JsonPropertyName("keywords")]
        public IReadOnlyList<TmdbKeyword> Keywords { get; init; } = [];
    }

    private sealed record GenresResponse
    {
        [JsonPropertyName("genres")]
        public IReadOnlyList<TmdbGenre> Genres { get; init; } = [];
    }
}

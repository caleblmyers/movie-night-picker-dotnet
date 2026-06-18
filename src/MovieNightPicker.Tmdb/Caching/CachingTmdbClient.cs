using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MovieNightPicker.Tmdb.Dtos;
using MovieNightPicker.Tmdb.Internal;

namespace MovieNightPicker.Tmdb.Caching;

/// <summary>
/// An <see cref="ITmdbClient"/> decorator that adds in-memory caching (per-category
/// TTLs from <see cref="TmdbCacheOptions"/>) and in-flight request de-duplication:
/// concurrent identical calls share a single fetch rather than each hitting TMDB.
/// </summary>
/// <remarks>
/// Caching is read-through. A miss runs the inner call exactly once — coalesced via
/// <see cref="_inFlight"/> so a burst of identical requests results in one network
/// fetch — then stores the non-null result for the category TTL. When
/// <see cref="TmdbCacheOptions.Enabled"/> is <c>false</c> every call passes straight
/// through to the inner client.
/// </remarks>
public sealed class CachingTmdbClient : ITmdbClient
{
    private readonly ITmdbClient _inner;
    private readonly IMemoryCache _cache;
    private readonly TmdbCacheOptions _options;

    // Keyed by cache key; the boxed value is the in-progress Task<T> for that key.
    private readonly ConcurrentDictionary<string, object> _inFlight = new();

    public CachingTmdbClient(ITmdbClient inner, IMemoryCache cache, IOptions<TmdbCacheOptions> options)
    {
        _inner = inner;
        _cache = cache;
        _options = options.Value;
    }

    public Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(
        string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"search:movie:{query}:{OptionsKey(options)}",
            _options.SearchTtl,
            () => _inner.SearchMoviesAsync(query, options, ct));

    public Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(
        DiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"discover:{DiscoverKey(discover)}:{OptionsKey(options)}",
            _options.SearchTtl,
            () => _inner.DiscoverMoviesAsync(discover, options, ct));

    public Task<TmdbMovie> GetMovieAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"movie:{id}:{OptionsKey(options)}",
            _options.DetailTtl,
            () => _inner.GetMovieAsync(id, options, ct));

    public Task<TmdbCredits> GetMovieCreditsAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"movie:credits:{id}:{OptionsKey(options)}",
            _options.CreditsTtl,
            () => _inner.GetMovieCreditsAsync(id, options, ct));

    public Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(
        int id, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"movie:keywords:{id}",
            _options.CreditsTtl,
            () => _inner.GetMovieKeywordsAsync(id, ct));

    public Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(
        TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"genres:{OptionsKey(options)}",
            _options.GenresTtl,
            () => _inner.GetGenresAsync(options, ct));

    public Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(
        string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"search:person:{query}:{OptionsKey(options)}",
            _options.SearchTtl,
            () => _inner.SearchPeopleAsync(query, options, ct));

    public Task<TmdbPerson> GetPersonAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"person:{id}:{OptionsKey(options)}",
            _options.DetailTtl,
            () => _inner.GetPersonAsync(id, options, ct));

    public Task<TmdbCredits> GetPersonCombinedCreditsAsync(
        int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
        GetOrFetchAsync(
            $"person:credits:{id}:{OptionsKey(options)}",
            _options.CreditsTtl,
            () => _inner.GetPersonCombinedCreditsAsync(id, options, ct));

    private async Task<T> GetOrFetchAsync<T>(string key, TimeSpan ttl, Func<Task<T>> fetch)
    {
        if (!_options.Enabled)
        {
            return await fetch().ConfigureAwait(false);
        }

        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        // Coalesce concurrent identical fetches onto the first in-flight task.
        var task = (Task<T>)_inFlight.GetOrAdd(key, _ => FetchAndCacheAsync(key, ttl, fetch));
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(new KeyValuePair<string, object>(key, task));
        }
    }

    private async Task<T> FetchAndCacheAsync<T>(string key, TimeSpan ttl, Func<Task<T>> fetch)
    {
        var result = await fetch().ConfigureAwait(false);
        if (result is not null)
        {
            _cache.Set(key, result, ttl);
        }

        return result;
    }

    // Reuse the query-string builder for a stable, collision-free representation of
    // the request inputs so two equivalent calls map to the same cache key.
    private static string OptionsKey(TmdbRequestOptions? options) =>
        TmdbQueryStringBuilder.ToQueryString(
            TmdbQueryStringBuilder.ForOptions(options ?? new TmdbRequestOptions()));

    private static string DiscoverKey(DiscoverParams discover) =>
        TmdbQueryStringBuilder.ToQueryString(TmdbQueryStringBuilder.ForDiscover(discover));
}

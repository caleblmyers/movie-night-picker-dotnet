using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Caching;
using MovieNightPicker.Tmdb.Dtos;

namespace MovieNightPicker.Tests.Tmdb;

public class CachingTmdbClientTests
{
    private static CachingTmdbClient Create(
        ITmdbClient inner, IMemoryCache? cache = null, TmdbCacheOptions? options = null) =>
        new(
            inner,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options ?? new TmdbCacheOptions()));

    [Fact]
    public async Task CacheHit_AvoidsSecondInnerCall()
    {
        var inner = new CountingTmdbClient();
        var sut = Create(inner);

        var first = await sut.GetMovieAsync(27205);
        var second = await sut.GetMovieAsync(27205);

        Assert.Equal(1, inner.GetMovieCalls);
        Assert.Equal(27205, first.Id);
        Assert.Same(first, second); // the cached instance is handed back, not a refetch
    }

    [Fact]
    public async Task DifferentArguments_AreCachedSeparately()
    {
        var inner = new CountingTmdbClient();
        var sut = Create(inner);

        await sut.GetMovieAsync(1);
        await sut.GetMovieAsync(2);
        await sut.GetMovieAsync(1);

        Assert.Equal(2, inner.GetMovieCalls); // id 1 cached after first call; id 2 is its own key
    }

    [Fact]
    public async Task CachingDisabled_AlwaysHitsInner()
    {
        var inner = new CountingTmdbClient();
        var sut = Create(inner, options: new TmdbCacheOptions { Enabled = false });

        await sut.GetMovieAsync(27205);
        await sut.GetMovieAsync(27205);

        Assert.Equal(2, inner.GetMovieCalls);
    }

    [Theory]
    [InlineData("genres", 24 * 60)]
    [InlineData("movie", 30)]
    [InlineData("credits", 60)]
    [InlineData("search", 5)]
    public async Task TtlCategory_MatchesTheCalledEndpoint(string category, int expectedMinutes)
    {
        var inner = new CountingTmdbClient();
        var cache = new RecordingMemoryCache();
        var sut = Create(inner, cache);

        switch (category)
        {
            case "genres":
                await sut.GetGenresAsync();
                break;
            case "movie":
                await sut.GetMovieAsync(27205);
                break;
            case "credits":
                await sut.GetMovieKeywordsAsync(27205);
                break;
            case "search":
                await sut.SearchMoviesAsync("inception");
                break;
        }

        var ttl = Assert.Single(cache.RecordedTtls);
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), ttl);
    }

    [Fact]
    public async Task ConcurrentIdenticalCalls_ShareOneFetch()
    {
        var inner = new CountingTmdbClient { Gate = new TaskCompletionSource() };
        var sut = Create(inner);

        // Both calls reach the in-flight registry before the inner fetch is allowed to
        // complete, so they must coalesce onto a single inner call.
        var first = sut.GetMovieAsync(27205);
        var second = sut.GetMovieAsync(27205);
        inner.Gate.SetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, inner.GetMovieCalls);
        Assert.Same(results[0], results[1]);
    }

    [Fact]
    public async Task AfterFetchCompletes_InFlightEntryIsReleased()
    {
        var inner = new CountingTmdbClient { Gate = new TaskCompletionSource() };
        var sut = Create(inner);

        var first = sut.GetMovieAsync(1);
        inner.Gate.SetResult();
        await first;

        // A later concurrent burst for a different id must not be blocked by a stale entry.
        inner.Gate = new TaskCompletionSource();
        var second = sut.GetMovieAsync(2);
        inner.Gate.SetResult();
        await second;

        Assert.Equal(2, inner.GetMovieCalls);
    }

    /// <summary>
    /// Counts inner calls per method and returns canned DTOs. An optional <see cref="Gate"/>
    /// lets a test hold a fetch open to exercise in-flight de-duplication.
    /// </summary>
    private sealed class CountingTmdbClient : ITmdbClient
    {
        public int GetMovieCalls;
        public int GenresCalls;
        public int KeywordsCalls;
        public int SearchCalls;

        public TaskCompletionSource? Gate { get; set; }

        private Task WaitGate() => Gate?.Task ?? Task.CompletedTask;

        public async Task<TmdbMovie> GetMovieAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default)
        {
            await WaitGate().ConfigureAwait(false);
            Interlocked.Increment(ref GetMovieCalls);
            return new TmdbMovie { Id = id, Title = $"Movie {id}" };
        }

        public async Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(TmdbRequestOptions? options = null, CancellationToken ct = default)
        {
            await WaitGate().ConfigureAwait(false);
            Interlocked.Increment(ref GenresCalls);
            return [new TmdbGenre { Id = 28, Name = "Action" }];
        }

        public async Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(int id, CancellationToken ct = default)
        {
            await WaitGate().ConfigureAwait(false);
            Interlocked.Increment(ref KeywordsCalls);
            return [new TmdbKeyword { Id = 818, Name = "based on novel" }];
        }

        public async Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default)
        {
            await WaitGate().ConfigureAwait(false);
            Interlocked.Increment(ref SearchCalls);
            return new TmdbPagedResult<TmdbMovie> { Page = 1, Results = [new TmdbMovie { Id = 1, Title = query }] };
        }

        public Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(DiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new TmdbPagedResult<TmdbMovie> { Page = 1 });

        public Task<TmdbCredits> GetMovieCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new TmdbCredits());

        public Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new TmdbPagedResult<TmdbPerson> { Page = 1 });

        public Task<TmdbPerson> GetPersonAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new TmdbPerson { Id = id });

        public Task<TmdbCredits> GetPersonCombinedCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new TmdbCredits());
    }

    /// <summary>
    /// An <see cref="IMemoryCache"/> that delegates to a real cache but records the
    /// relative TTL set on each committed entry, so tests can assert the category TTL.
    /// </summary>
    private sealed class RecordingMemoryCache : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());

        public List<TimeSpan?> RecordedTtls { get; } = [];

        public ICacheEntry CreateEntry(object key) =>
            new RecordingEntry(_inner.CreateEntry(key), ttl => RecordedTtls.Add(ttl));

        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);

        public void Remove(object key) => _inner.Remove(key);

        public void Dispose() => _inner.Dispose();
    }

    private sealed class RecordingEntry : ICacheEntry
    {
        private readonly ICacheEntry _inner;
        private readonly Action<TimeSpan?> _onCommit;

        public RecordingEntry(ICacheEntry inner, Action<TimeSpan?> onCommit)
        {
            _inner = inner;
            _onCommit = onCommit;
        }

        public object Key => _inner.Key;
        public object? Value { get => _inner.Value; set => _inner.Value = value; }
        public DateTimeOffset? AbsoluteExpiration { get => _inner.AbsoluteExpiration; set => _inner.AbsoluteExpiration = value; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get => _inner.AbsoluteExpirationRelativeToNow; set => _inner.AbsoluteExpirationRelativeToNow = value; }
        public TimeSpan? SlidingExpiration { get => _inner.SlidingExpiration; set => _inner.SlidingExpiration = value; }
        public IList<IChangeToken> ExpirationTokens => _inner.ExpirationTokens;
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks => _inner.PostEvictionCallbacks;
        public CacheItemPriority Priority { get => _inner.Priority; set => _inner.Priority = value; }
        public long? Size { get => _inner.Size; set => _inner.Size = value; }

        // Dispose is when MemoryCache commits the entry — capture the TTL at that point.
        public void Dispose()
        {
            _onCommit(_inner.AbsoluteExpirationRelativeToNow);
            _inner.Dispose();
        }
    }
}

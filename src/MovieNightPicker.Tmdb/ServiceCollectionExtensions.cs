using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Caching;

// Placed in the DI namespace so callers get AddTmdbClient via a single using.
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITmdbClient"/> backed by a typed <see cref="System.Net.Http.HttpClient"/>
    /// and wrapped in a <see cref="CachingTmdbClient"/> for in-memory caching plus in-flight
    /// request de-duplication. <see cref="TmdbClientOptions"/> is bound from
    /// <paramref name="configureOptions"/>; caching is on by default and can be tuned (or
    /// disabled) via the optional <paramref name="configureCache"/> delegate.
    /// </summary>
    public static IServiceCollection AddTmdbClient(
        this IServiceCollection services,
        Action<TmdbClientOptions> configureOptions,
        Action<TmdbCacheOptions>? configureCache = null)
    {
        services.Configure(configureOptions);
        if (configureCache is not null)
        {
            services.Configure(configureCache);
        }

        services.AddMemoryCache();

        // The concrete typed client does the HTTP work; the decorator (registered as
        // ITmdbClient) layers caching + de-dup on top of it. The decorator is a singleton
        // (it owns the shared cache + in-flight registry), but it resolves the inner
        // TmdbClient via a factory per call so each fetch gets a fresh, factory-managed
        // HttpClient — capturing one instance would pin its handler and defeat rotation.
        services.AddHttpClient<TmdbClient>();
        services.AddSingleton<ITmdbClient>(sp => new CachingTmdbClient(
            () => sp.GetRequiredService<TmdbClient>(),
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<IOptions<TmdbCacheOptions>>()));

        return services;
    }
}

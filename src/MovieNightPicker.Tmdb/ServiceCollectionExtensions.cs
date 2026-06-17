using Microsoft.Extensions.DependencyInjection;
using MovieNightPicker.Tmdb;

// Placed in the DI namespace so callers get AddTmdbClient via a single using.
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITmdbClient"/> as a typed <see cref="HttpClient"/> and binds
    /// <see cref="TmdbClientOptions"/> from the supplied configuration delegate.
    /// </summary>
    public static IServiceCollection AddTmdbClient(
        this IServiceCollection services, Action<TmdbClientOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddHttpClient<ITmdbClient, TmdbClient>();
        return services;
    }
}

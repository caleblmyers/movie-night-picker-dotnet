using MovieNightPicker.Api.Adapters;
using MovieNightPicker.Core;
using MovieNightPicker.Data;

namespace MovieNightPicker.Api.Extensions;

/// <summary>
/// Composition root for the API: wires the TMDB client, the data layer, and the
/// Core-facing adapter into a single <c>AddAppServices</c> call.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddTmdbClient(o => o.ApiKey = config["Tmdb:ApiKey"] ?? string.Empty);

        // Connection string is empty in tests / un-configured environments; the
        // DbContext is only resolved when a request actually touches the database,
        // so registering with an empty string here is harmless.
        services.AddData(config.GetConnectionString("Default") ?? string.Empty);

        services.AddScoped<IMovieDataSource, TmdbMovieDataSource>();

        return services;
    }
}

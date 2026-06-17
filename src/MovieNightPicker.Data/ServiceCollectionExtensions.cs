using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MovieNightPicker.Data;

/// <summary>
/// DI registration helpers for the data layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MovieNightPickerDbContext"/> backed by PostgreSQL (Npgsql).
    /// </summary>
    public static IServiceCollection AddData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MovieNightPickerDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MovieNightPicker.Data;

/// <summary>
/// Lets the EF Core CLI (migrations, scaffolding) build the context without the
/// API host. Reads the connection string from <c>ConnectionStrings__Default</c>
/// and falls back to a localhost placeholder for design-time use only.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MovieNightPickerDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Database=movienightpicker;Username=postgres;Password=postgres";

    public MovieNightPickerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<MovieNightPickerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MovieNightPickerDbContext(options);
    }
}

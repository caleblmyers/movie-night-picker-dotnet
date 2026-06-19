using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MovieNightPicker.Api.Adapters;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Services;
using MovieNightPicker.Core;
using MovieNightPicker.Data;

namespace MovieNightPicker.Api.Extensions;

/// <summary>
/// Composition root for the API: wires the TMDB client, the data layer, the
/// Core-facing adapter, JWT authentication, and the feature services into a single
/// <c>AddAppServices</c> call.
/// </summary>
public static class ServiceCollectionExtensions
{
    // Insecure, well-known fallback so the app boots in Development without a configured
    // secret. Never used outside Development — see the guard below.
    private const string DevelopmentSigningKey =
        "dev-only-insecure-jwt-signing-key-do-not-use-in-production";

    public static IServiceCollection AddAppServices(
        this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        services.AddTmdbClient(o => o.ApiKey = config["Tmdb:ApiKey"] ?? string.Empty);

        // Connection string is empty in tests / un-configured environments; the
        // DbContext is only resolved when a request actually touches the database,
        // so registering with an empty string here is harmless.
        services.AddData(config.GetConnectionString("Default") ?? string.Empty);

        services.AddScoped<IMovieDataSource, TmdbMovieDataSource>();

        AddAuth(services, config, environment);

        // Feature services (scoped — they take the scoped DbContext / TMDB client).
        services.AddScoped<CollectionService>();
        services.AddScoped<RatingReviewService>();
        services.AddScoped<InsightsService>();

        return services;
    }

    private static void AddAuth(
        IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        var jwtSection = config.GetSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        // A real signing key must come from config (env/user-secrets). Only Development
        // is allowed to fall back to the well-known dev key so the app still boots.
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) && environment.IsDevelopment())
        {
            jwtOptions.SigningKey = DevelopmentSigningKey;
        }

        // Bind options, then ensure the (possibly dev-fallback) signing key is the one
        // JwtTokenService issues with — keeping issuance and validation in lock-step.
        services.Configure<JwtOptions>(jwtSection);
        services.PostConfigure<JwtOptions>(o => o.SigningKey = jwtOptions.SigningKey);

        services.AddSingleton<PasswordHasher>();
        services.AddScoped<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                };
            });

        services.AddAuthorization();
    }
}

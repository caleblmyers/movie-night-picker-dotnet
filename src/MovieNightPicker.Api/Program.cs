using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MovieNightPicker.Api.Endpoints;
using MovieNightPicker.Api.Extensions;
using MovieNightPicker.Tmdb;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// TMDB client, data layer, the Core-facing adapter, auth, and feature services.
builder.Services.AddAppServices(builder.Configuration, builder.Environment);

// RFC7807 problem-details responses for unhandled exceptions.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(MovieNightPicker.Api.Extensions.ServiceCollectionExtensions.WebClientCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Liveness probe — replaced by real endpoints as features land (see .claude-knowledge/todos.md).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

app.MapMovieEndpoints();
app.MapPersonEndpoints();
app.MapAuthEndpoints();
app.MapCollectionEndpoints();
app.MapRatingEndpoints();
app.MapReviewEndpoints();
app.MapSuggestEndpoints();
app.MapInsightsEndpoints();

app.Run();

/// <summary>
/// Translates unhandled exceptions into RFC7807 ProblemDetails responses:
/// a <see cref="TmdbApiException"/> becomes a 502 Bad Gateway (the upstream
/// movie service failed), everything else a generic 500.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            TmdbApiException tmdb => new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Upstream movie service error",
                Detail = tmdb.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
            },
        };

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;

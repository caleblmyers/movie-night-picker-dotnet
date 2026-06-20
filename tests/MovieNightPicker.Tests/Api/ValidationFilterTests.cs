using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Validation;
using MovieNightPicker.Data;

namespace MovieNightPicker.Tests.Api;

/// <summary>
/// Tests for <see cref="ValidationEndpointFilter{T}"/>: the filter runs DataAnnotations
/// on the request DTO and short-circuits with a <see cref="ValidationProblem"/> on
/// invalid input, otherwise calling through. Includes an end-to-end check that the
/// filter is actually wired onto the rating/review upsert routes.
/// </summary>
public class ValidationFilterTests
{
    private const string NextSentinel = "next-was-called";

    /// <summary>Runs the filter against a single DTO argument with a sentinel-returning next.</summary>
    private static async Task<object?> RunFilterAsync<T>(T model)
        where T : class
    {
        var filter = new ValidationEndpointFilter<T>();
        var httpContext = new DefaultHttpContext();
        var context = EndpointFilterInvocationContext.Create(httpContext, model);

        return await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(NextSentinel));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-3)]
    public async Task Filter_rejects_out_of_range_rating(int value)
    {
        var result = await RunFilterAsync(new UpsertRatingRequest(value));

        Assert.IsType<ValidationProblem>(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    public async Task Filter_passes_valid_rating_through(int value)
    {
        var result = await RunFilterAsync(new UpsertRatingRequest(value));

        Assert.Equal(NextSentinel, result);
    }

    [Fact]
    public async Task Filter_rejects_empty_review_content()
    {
        var result = await RunFilterAsync(new UpsertReviewRequest(string.Empty));

        Assert.IsType<ValidationProblem>(result);
    }

    [Fact]
    public async Task Filter_passes_valid_review_through()
    {
        var result = await RunFilterAsync(new UpsertReviewRequest("Loved it"));

        Assert.Equal(NextSentinel, result);
    }

    [Fact]
    public async Task Filter_reports_the_offending_member()
    {
        var result = await RunFilterAsync(new UpsertRatingRequest(99));

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Contains("Value", problem.ProblemDetails.Errors.Keys);
    }

    /// <summary>
    /// End-to-end: proves <c>.WithRequestValidation&lt;UpsertRatingRequest&gt;()</c> is
    /// wired onto the route — an out-of-range value returns 400 from the host, not 200.
    /// </summary>
    [Fact]
    public async Task Route_returns_400_for_out_of_range_rating()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                var stale = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(DbContextOptions<MovieNightPickerDbContext>)
                        || (d.ServiceType.IsGenericType
                            && d.ServiceType.Name.Contains("DbContextOptionsConfiguration")
                            && d.ServiceType.GetGenericArguments()[0] == typeof(MovieNightPickerDbContext)))
                    .ToList();
                foreach (var descriptor in stale)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<MovieNightPickerDbContext>(o => o.UseSqlite(connection));
            });
        });

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<MovieNightPickerDbContext>().Database.EnsureCreated();
        }

        using var http = factory.CreateClient();

        var register = await http.PostAsJsonAsync(
            "/auth/register",
            new { email = "validation@example.com", password = "password123", name = "Validation User" });
        var auth = await register.Content.ReadFromJsonAsync<AuthTokenDto>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await http.PutAsJsonAsync("/ratings/603", new { value = 99 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record AuthTokenDto(string Token);
}

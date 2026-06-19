using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieNightPicker.Data;

namespace MovieNightPicker.Tests.Api;

/// <summary>
/// End-to-end auth wiring through the real host (<see cref="WebApplicationFactory{Program}"/>):
/// a user-scoped route is protected, and register -> authorized-call succeeds. Runs against a
/// shared in-memory SQLite database and the Development JWT signing-key fallback.
/// </summary>
public class AuthFlowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;

    public AuthFlowTests()
    {
        // A single open connection keeps the in-memory database alive for the host's lifetime.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Development => the JWT dev-fallback signing key is used for both issuance
            // and validation (no secret committed / needed for the test).
            builder.UseEnvironment("Development");

            builder.ConfigureTestServices(services =>
            {
                // Swap the Npgsql DbContext for the shared in-memory SQLite one. EF Core
                // applies every registered options-configuration, so the Npgsql one must be
                // removed too — otherwise both providers end up on the same context. The
                // configuration interface is internal, so match it by type name.
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

                services.AddDbContext<MovieNightPickerDbContext>(o => o.UseSqlite(_connection));
            });
        });

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<MovieNightPickerDbContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task Protected_route_returns_401_without_token()
    {
        using var http = _factory.CreateClient();

        var response = await http.GetAsync("/collections");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_then_authorized_call_succeeds()
    {
        using var http = _factory.CreateClient();

        var register = await http.PostAsJsonAsync(
            "/auth/register",
            new { email = "flow@example.com", password = "password123", name = "Flow User" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthDto>();
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var collections = await http.GetAsync("/collections");

        Assert.Equal(HttpStatusCode.OK, collections.StatusCode);
    }

    [Fact]
    public async Task Login_after_register_yields_working_token()
    {
        using var http = _factory.CreateClient();

        await http.PostAsJsonAsync(
            "/auth/register",
            new { email = "login-flow@example.com", password = "password123", name = "Login Flow" });

        var login = await http.PostAsJsonAsync(
            "/auth/login",
            new { email = "login-flow@example.com", password = "password123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>();

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        var collections = await http.GetAsync("/collections");

        Assert.Equal(HttpStatusCode.OK, collections.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record AuthDto(string Token, AuthUserDto User);
    private sealed record AuthUserDto(int Id, string Email, string Name);
}

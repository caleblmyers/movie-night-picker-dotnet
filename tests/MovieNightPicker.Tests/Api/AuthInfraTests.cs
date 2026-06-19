using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Endpoints;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Tests.Api;

/// <summary>
/// Direct service/handler tests for the auth foundation (no WebApplicationFactory —
/// host wiring is exercised by task-002). Uses an open in-memory SQLite connection
/// so the schema is real but disposable.
/// </summary>
public class AuthInfraTests
{
    private static JwtTokenService TokenService() =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-that-is-long-enough-for-hs256",
            TokenLifetimeMinutes = 30,
        }));

    /// <summary>Opens a fresh in-memory SQLite DbContext with the schema created.</summary>
    private static (MovieNightPickerDbContext Db, SqliteConnection Conn) CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<MovieNightPickerDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new MovieNightPickerDbContext(options);
        db.Database.EnsureCreated();
        return (db, conn);
    }

    [Fact]
    public void PasswordHasher_round_trips_and_rejects_wrong_password()
    {
        var hasher = new PasswordHasher();

        var hash = hasher.Hash("correct horse battery staple");

        Assert.NotEqual("correct horse battery staple", hash);
        Assert.True(hasher.Verify("correct horse battery staple", hash));
        Assert.False(hasher.Verify("wrong password", hash));
    }

    [Fact]
    public void PasswordHasher_produces_distinct_hashes_for_same_password()
    {
        var hasher = new PasswordHasher();

        // Random per-hash salt means two hashes of the same password differ.
        Assert.NotEqual(hasher.Hash("same"), hasher.Hash("same"));
    }

    [Fact]
    public void JwtTokenService_issues_token_whose_user_id_round_trips()
    {
        var user = new User { Id = 42, Email = "user@example.com", Password = "x", Name = "User" };

        var token = TokenService().CreateToken(user);

        // Read the raw claims (no inbound mapping) and resolve via GetUserId.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(handler.ReadJwtToken(token).Claims));

        Assert.Equal(42, principal.GetUserId());
        Assert.Contains(principal.Claims, c => c.Value == "user@example.com");
    }

    [Fact]
    public void GetUserId_returns_null_when_claim_missing()
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        Assert.Null(principal.GetUserId());
    }

    [Fact]
    public async Task Register_then_login_succeeds_and_returns_token()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var hasher = new PasswordHasher();
        var tokens = TokenService();

        var register = await AuthEndpoints.RegisterAsync(
            new RegisterRequest("New@Example.com", "password123", "New User"),
            db, hasher, tokens, default);

        var ok = Assert.IsType<Ok<AuthResponse>>(register.Result);
        Assert.False(string.IsNullOrWhiteSpace(ok.Value!.Token));
        Assert.Equal("new@example.com", ok.Value.User.Email); // normalized to lowercase

        var login = await AuthEndpoints.LoginAsync(
            new LoginRequest("new@example.com", "password123"), db, hasher, tokens, default);

        var loginOk = Assert.IsType<Ok<AuthResponse>>(login.Result);
        Assert.False(string.IsNullOrWhiteSpace(loginOk.Value!.Token));
        Assert.Equal(ok.Value.User.Id, loginOk.Value.User.Id);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var hasher = new PasswordHasher();
        var tokens = TokenService();

        var first = await AuthEndpoints.RegisterAsync(
            new RegisterRequest("dup@example.com", "password123", "First"), db, hasher, tokens, default);
        Assert.IsType<Ok<AuthResponse>>(first.Result);

        // Same email, different case — still a conflict after normalization.
        var second = await AuthEndpoints.RegisterAsync(
            new RegisterRequest("DUP@example.com", "password123", "Second"), db, hasher, tokens, default);

        Assert.IsType<Conflict<ErrorResponse>>(second.Result);
    }

    [Fact]
    public async Task Register_validates_input()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var hasher = new PasswordHasher();
        var tokens = TokenService();

        var badEmail = await AuthEndpoints.RegisterAsync(
            new RegisterRequest("not-an-email", "password123", "Name"), db, hasher, tokens, default);
        Assert.IsType<ValidationProblem>(badEmail.Result);

        var shortPassword = await AuthEndpoints.RegisterAsync(
            new RegisterRequest("a@b.com", "short", "Name"), db, hasher, tokens, default);
        Assert.IsType<ValidationProblem>(shortPassword.Result);
    }

    [Fact]
    public async Task Login_rejects_unknown_user_and_bad_password()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var hasher = new PasswordHasher();
        var tokens = TokenService();

        await AuthEndpoints.RegisterAsync(
            new RegisterRequest("real@example.com", "password123", "Real"), db, hasher, tokens, default);

        var unknown = await AuthEndpoints.LoginAsync(
            new LoginRequest("nobody@example.com", "password123"), db, hasher, tokens, default);
        Assert.IsType<UnauthorizedHttpResult>(unknown.Result);

        var wrongPassword = await AuthEndpoints.LoginAsync(
            new LoginRequest("real@example.com", "wrongpass"), db, hasher, tokens, default);
        Assert.IsType<UnauthorizedHttpResult>(wrongPassword.Result);
    }
}

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;

[assembly: InternalsVisibleTo("MovieNightPicker.Tests")]

namespace MovieNightPicker.Api.Endpoints;

/// <summary>
/// Authentication HTTP surface: register and login, both returning a signed JWT.
/// These endpoints are anonymous (they mint the tokens the rest of the API requires).
/// Handlers return typed results so they can be unit-tested without a host.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapPost("/register", RegisterAsync).WithName("Register");
        auth.MapPost("/login", LoginAsync).WithName("Login");

        return app;
    }

    /// <summary>Registers a new user (<c>POST /auth/register</c>) and returns a JWT.</summary>
    internal static async Task<Results<Ok<AuthResponse>, ValidationProblem, Conflict<ErrorResponse>>> RegisterAsync(
        RegisterRequest body,
        MovieNightPickerDbContext db,
        PasswordHasher hasher,
        JwtTokenService tokens,
        CancellationToken ct)
    {
        var email = body.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["A valid email is required."],
            });
        }

        if (string.IsNullOrEmpty(body.Password) || body.Password.Length < 8)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["Password must be at least 8 characters."],
            });
        }

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."],
            });
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return TypedResults.Conflict(new ErrorResponse("A user with that email already exists."));
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = email,
            Password = hasher.Hash(body.Password),
            Name = body.Name.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new AuthResponse(tokens.CreateToken(user), AuthUser.FromEntity(user)));
    }

    /// <summary>Logs an existing user in (<c>POST /auth/login</c>); 401 on bad credentials.</summary>
    internal static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest body,
        MovieNightPickerDbContext db,
        PasswordHasher hasher,
        JwtTokenService tokens,
        CancellationToken ct)
    {
        var email = body.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !hasher.Verify(body.Password ?? string.Empty, user.Password))
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new AuthResponse(tokens.CreateToken(user), AuthUser.FromEntity(user)));
    }
}

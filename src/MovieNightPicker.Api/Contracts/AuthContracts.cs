using System.ComponentModel.DataAnnotations;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Api.Contracts;

/// <summary>Request body for <c>POST /auth/register</c>.</summary>
public sealed record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password,
    [property: Required] string Name);

/// <summary>Request body for <c>POST /auth/login</c>.</summary>
public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

/// <summary>Basic, non-sensitive user info returned alongside a token.</summary>
public sealed record AuthUser(int Id, string Email, string Name)
{
    public static AuthUser FromEntity(User user) => new(user.Id, user.Email, user.Name);
}

/// <summary>Successful auth response: a signed JWT plus the user it belongs to.</summary>
public sealed record AuthResponse(string Token, AuthUser User);

/// <summary>A simple error payload for auth failures (e.g. duplicate email).</summary>
public sealed record ErrorResponse(string Error);

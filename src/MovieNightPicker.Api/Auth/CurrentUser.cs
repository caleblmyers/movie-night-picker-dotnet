using System.Security.Claims;

namespace MovieNightPicker.Api.Auth;

/// <summary>
/// Resolves the current user from the authenticated principal. User-scoped endpoints
/// call <see cref="GetUserId"/> to scope every query to the caller.
/// </summary>
public static class CurrentUser
{
    /// <summary>
    /// Reads the user id from the <see cref="ClaimTypes.NameIdentifier"/> claim,
    /// or null when unauthenticated / the claim is missing or malformed.
    /// </summary>
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}

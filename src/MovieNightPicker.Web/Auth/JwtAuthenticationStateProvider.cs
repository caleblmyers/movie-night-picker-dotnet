using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace MovieNightPicker.Web.Auth;

/// <summary>
/// Drives Blazor's auth state from the JWT in <see cref="TokenStore"/>: decodes the
/// token's payload for the current user's claims, and exposes notify hooks the auth
/// client calls after login / logout. The payload is read for display only — the API
/// is the real authority and validates the signature on every request.
/// </summary>
public sealed class JwtAuthenticationStateProvider(TokenStore tokens) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await tokens.GetAsync();
        var claims = ReadClaims(token);
        if (claims is null || IsExpired(claims))
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Call after a successful login/register so the UI re-renders as authenticated.</summary>
    public void NotifyAuthenticated() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    /// <summary>Call after logout to drop back to anonymous.</summary>
    public void NotifyLoggedOut() =>
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));

    /// <summary>
    /// Decode the JWT payload (middle base64url segment) into claims. Returns null
    /// for a missing/malformed token.
    /// </summary>
    private static IReadOnlyList<Claim>? ReadClaims(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var json = Base64UrlDecode(parts[1]);
            var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            return map is null
                ? null
                : map.Select(kv => new Claim(kv.Key, kv.Value.ToString())).ToList();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static bool IsExpired(IEnumerable<Claim> claims)
    {
        var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        return long.TryParse(exp, out var seconds)
            && DateTimeOffset.FromUnixTimeSeconds(seconds) < DateTimeOffset.UtcNow;
    }

    private static byte[] Base64UrlDecodeBytes(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch { 2 => s + "==", 3 => s + "=", _ => s };
        return Convert.FromBase64String(s);
    }

    private static string Base64UrlDecode(string input) =>
        System.Text.Encoding.UTF8.GetString(Base64UrlDecodeBytes(input));
}

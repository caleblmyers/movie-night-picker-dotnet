using System.Net.Http.Json;

namespace MovieNightPicker.Web.Auth;

/// <summary>Request/response contracts for the API's <c>/auth</c> endpoints.</summary>
public sealed record RegisterRequest(string Email, string Password, string Name);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, int UserId, string Email, string Name);

/// <summary>
/// Talks to the API's auth endpoints and, on success, stores the JWT and notifies
/// the auth state provider so the whole app re-renders as authenticated.
/// </summary>
public sealed class AuthClient(
    HttpClient http, TokenStore tokens, JwtAuthenticationStateProvider authState)
{
    /// <summary>Register a new account. Returns null on failure (e.g. duplicate email).</summary>
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/auth/register", request, ct);
        return await CompleteAsync(response, ct);
    }

    /// <summary>Log in. Returns null on bad credentials.</summary>
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/auth/login", request, ct);
        return await CompleteAsync(response, ct);
    }

    public async Task LogoutAsync()
    {
        await tokens.ClearAsync();
        authState.NotifyLoggedOut();
    }

    private async Task<AuthResponse?> CompleteAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        if (auth is not null)
        {
            await tokens.SetAsync(auth.Token);
            authState.NotifyAuthenticated();
        }

        return auth;
    }
}

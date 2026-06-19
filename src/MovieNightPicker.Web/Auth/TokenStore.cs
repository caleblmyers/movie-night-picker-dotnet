using Microsoft.JSInterop;

namespace MovieNightPicker.Web.Auth;

/// <summary>
/// Persists the JWT in the browser's localStorage via JS interop. No third-party
/// package — just the built-in <see cref="IJSRuntime"/>.
/// </summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string Key = "mnp.token";

    public async ValueTask<string?> GetAsync() =>
        await js.InvokeAsync<string?>("localStorage.getItem", Key);

    public async ValueTask SetAsync(string token) =>
        await js.InvokeVoidAsync("localStorage.setItem", Key, token);

    public async ValueTask ClearAsync() =>
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
}

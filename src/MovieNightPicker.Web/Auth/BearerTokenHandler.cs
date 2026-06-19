using System.Net.Http.Headers;

namespace MovieNightPicker.Web.Auth;

/// <summary>
/// Attaches the stored JWT as a Bearer token to every outgoing API request, so
/// feature components can just inject <see cref="HttpClient"/> and call the API.
/// </summary>
public sealed class BearerTokenHandler(TokenStore tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

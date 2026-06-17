using System.Net;
using System.Text;

namespace MovieNightPicker.Tests.Tmdb;

/// <summary>
/// A hand-rolled <see cref="HttpMessageHandler"/> that returns a single canned
/// response and records the URI it was asked for. Keeps the TMDB client tests off
/// the network without pulling in a mocking package.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _body;

    /// <summary>The URI of the most recent request — used to assert URL building.</summary>
    public Uri? LastRequestUri { get; private set; }

    public StubHttpMessageHandler(HttpStatusCode statusCode, string body)
    {
        _statusCode = statusCode;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json"),
        };

        return Task.FromResult(response);
    }
}

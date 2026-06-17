namespace MovieNightPicker.Tmdb;

/// <summary>
/// Thrown when TMDB returns a non-success HTTP status. <see cref="Exception.Message"/>
/// always has the form <c>"TMDB API error: {statusCode} - {reason}"</c>, where
/// <c>reason</c> is TMDB's <c>status_message</c> when present, otherwise the HTTP
/// reason phrase.
/// </summary>
public sealed class TmdbApiException : Exception
{
    /// <summary>The numeric HTTP status code that triggered the failure (0 if unknown).</summary>
    public int StatusCode { get; }

    public TmdbApiException(string message, int statusCode = 0)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

namespace MovieNightPicker.Tmdb;

/// <summary>
/// Configuration for <see cref="TmdbClient"/>, bound via the options pattern.
/// The API key comes from user-secrets / environment — never commit it.
/// </summary>
public class TmdbClientOptions
{
    /// <summary>TMDB API v3 key. Required.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL of the TMDB REST API (no trailing slash needed).</summary>
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3";
}

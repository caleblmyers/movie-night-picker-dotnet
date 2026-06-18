namespace MovieNightPicker.Tmdb.Caching;

/// <summary>
/// Per-category time-to-live settings for <see cref="CachingTmdbClient"/>. The
/// defaults mirror the original app: rarely-changing reference data (genres) is
/// cached for a day, detail lookups for half an hour, credits/keywords for an hour,
/// and volatile search results for only five minutes.
/// </summary>
public sealed class TmdbCacheOptions
{
    /// <summary>Master switch — when <c>false</c> the decorator passes every call straight through.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>TTL for the genre list (<c>/genre/movie/list</c>).</summary>
    public TimeSpan GenresTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>TTL for movie and person detail lookups.</summary>
    public TimeSpan DetailTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>TTL for credits and keyword lookups.</summary>
    public TimeSpan CreditsTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>TTL for search and discover results.</summary>
    public TimeSpan SearchTtl { get; set; } = TimeSpan.FromMinutes(5);
}

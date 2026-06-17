namespace MovieNightPicker.Tmdb.Internal;

/// <summary>
/// Turns the strongly-typed request option records into TMDB query strings.
/// Kept <c>public</c> (despite the <c>Internal</c> folder) so the test project can
/// exercise the exact mappings without an <c>InternalsVisibleTo</c> hook.
/// </summary>
/// <remarks>
/// Every method skips null / empty values so an unset field never emits a spurious
/// query key. Array fields collapse to comma-joined id lists (TMDB's "AND/OR" form).
/// Values are escaped with <see cref="Uri.EscapeDataString"/>; the fixed key literals
/// are safe as-is.
/// </remarks>
public static class TmdbQueryStringBuilder
{
    /// <summary>
    /// Builds the full query string (leading <c>?</c>) for a <c>/discover/movie</c>
    /// call: the discover params, the common request options, then <c>api_key</c>.
    /// </summary>
    public static string BuildDiscoverQuery(DiscoverParams discover, TmdbRequestOptions? options, string apiKey)
    {
        var pairs = ForDiscover(discover)
            .Concat(ForOptions(options ?? new TmdbRequestOptions()))
            .Append(Pair("api_key", apiKey));
        return ToQueryString(pairs);
    }

    /// <summary>
    /// Builds a query string from the common request options plus <c>api_key</c> and
    /// any endpoint-specific extras. Used by non-discover endpoints (search, details).
    /// </summary>
    public static string BuildOptionsQuery(TmdbRequestOptions? options, string apiKey, IEnumerable<KeyValuePair<string, string?>>? extras = null)
    {
        var pairs = ForOptions(options ?? new TmdbRequestOptions())
            .Concat(extras ?? [])
            .Append(Pair("api_key", apiKey));
        return ToQueryString(pairs);
    }

    /// <summary>Maps every <see cref="DiscoverParams"/> field to its TMDB query key(s).</summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> ForDiscover(DiscoverParams p)
    {
        var pairs = new List<KeyValuePair<string, string?>>
        {
            Pair("with_genres", JoinIds(p.Genres)),
            Pair("without_genres", JoinIds(p.ExcludeGenres)),
            Pair("with_cast", JoinIds(p.Actors)),
            Pair("with_crew", JoinIds(p.Crew)),
            Pair("without_cast", JoinIds(p.ExcludeCast)),
            Pair("without_crew", JoinIds(p.ExcludeCrew)),
            Pair("with_keywords", JoinIds(p.Keywords)),
            Pair("with_origin_country", Join(p.OriginCountries)),
            Pair("with_watch_providers", p.WatchProviders),
            Pair("vote_average.gte", Format(p.VoteAverageGte)),
            Pair("vote_count.gte", Format(p.VoteCountGte)),
        };

        if (p.YearRange is { } years)
        {
            pairs.Add(Pair("primary_release_date.gte", $"{years.Start:D4}-01-01"));
            pairs.Add(Pair("primary_release_date.lte", $"{years.End:D4}-12-31"));
        }

        if (p.RuntimeRange is { } runtime)
        {
            pairs.Add(Pair("with_runtime.gte", Format(runtime.Start)));
            pairs.Add(Pair("with_runtime.lte", Format(runtime.End)));
        }

        if (p.PopularityRange is { } popularity)
        {
            pairs.Add(Pair("popularity.gte", Format(popularity.Start)));
            pairs.Add(Pair("popularity.lte", Format(popularity.End)));
        }

        return pairs;
    }

    /// <summary>Maps the common <see cref="TmdbRequestOptions"/> to TMDB query keys.</summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> ForOptions(TmdbRequestOptions o) =>
    [
        Pair("language", o.Language),
        Pair("region", o.Region),
        Pair("sort_by", o.SortBy),
        Pair("page", o.Page.ToString()),
        Pair("include_adult", o.IncludeAdult ? "true" : "false"),
        Pair("year", Format(o.Year)),
        Pair("primary_release_year", Format(o.PrimaryReleaseYear)),
        Pair("vote_average.gte", Format(o.VoteAverageGte)),
        Pair("vote_count.gte", Format(o.VoteCountGte)),
        Pair("with_original_language", o.WithOriginalLanguage),
        Pair("with_watch_providers", o.WithWatchProviders),
        Pair("popularity.gte", Format(o.PopularityGte)),
        Pair("popularity.lte", Format(o.PopularityLte)),
    ];

    /// <summary>
    /// Renders key/value pairs as a query string, dropping any pair with a null or
    /// empty value and URL-escaping the remaining values.
    /// </summary>
    public static string ToQueryString(IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));

        return query.Length == 0 ? string.Empty : "?" + query;
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value) => new(key, value);

    private static string? JoinIds(IReadOnlyCollection<int> ids) =>
        ids.Count == 0 ? null : string.Join(",", ids);

    private static string? Join(IReadOnlyCollection<string> values) =>
        values.Count == 0 ? null : string.Join(",", values);

    // Invariant culture so "6.5" never becomes "6,5" on a non-US machine.
    private static string? Format(double? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? Format(int? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

namespace MovieNightPicker.Core.Constants;

/// <summary>
/// Bidirectional TMDB movie-genre id &lt;-&gt; name map. The canonical TMDB
/// genre list (the same ids the discover endpoint expects).
/// </summary>
public static class Genres
{
    /// <summary>Genre id -> display name.</summary>
    public static readonly IReadOnlyDictionary<int, string> ById = new Dictionary<int, string>
    {
        [28] = "Action",
        [12] = "Adventure",
        [16] = "Animation",
        [35] = "Comedy",
        [80] = "Crime",
        [99] = "Documentary",
        [18] = "Drama",
        [10751] = "Family",
        [14] = "Fantasy",
        [36] = "History",
        [27] = "Horror",
        [10402] = "Music",
        [9648] = "Mystery",
        [10749] = "Romance",
        [878] = "Sci-Fi",
        [10770] = "TV Movie",
        [53] = "Thriller",
        [10752] = "War",
        [37] = "Western",
    };

    /// <summary>Display name -> genre id (case-insensitive), derived from <see cref="ById"/>.</summary>
    public static readonly IReadOnlyDictionary<string, int> ByName =
        ById.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Look up a genre name by id, or null if unknown.</summary>
    public static string? NameOf(int id) => ById.GetValueOrDefault(id);

    /// <summary>Look up a genre id by name (case-insensitive), or null if unknown.</summary>
    public static int? IdOf(string name) =>
        ByName.TryGetValue(name, out var id) ? id : null;
}

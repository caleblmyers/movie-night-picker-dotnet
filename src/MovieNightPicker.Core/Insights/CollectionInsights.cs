using MovieNightPicker.Core.Constants;

namespace MovieNightPicker.Core.Insights;

/// <summary>
/// Aggregates a collection of enriched movies into headline statistics — genre,
/// keyword, actor, and director/writer frequency tables plus year-range and
/// rating/runtime averages. Pure function, all LINQ; empty input yields a valid
/// zeroed result rather than throwing.
/// </summary>
public static class CollectionInsights
{
    /// <summary>Crew is kept only when it directs or writes — by job or department.</summary>
    private static readonly HashSet<string> CrewJobs =
        new(StringComparer.OrdinalIgnoreCase) { "Director", "Writer", "Screenplay", "Story" };

    private static readonly HashSet<string> CrewDepartments =
        new(StringComparer.OrdinalIgnoreCase) { "Directing", "Writing" };

    private const int TopKeywordLimit = 20;
    private const int TopPeopleLimit = 10;

    /// <summary>Compute the aggregate insights for <paramref name="movies"/>.</summary>
    public static CollectionInsightsResult Compute(IReadOnlyList<InsightsMovie> movies)
    {
        var genres = movies
            .SelectMany(m => m.Movie.Genres)
            .GroupBy(id => id)
            .Select(g => new GenreCount(g.Key, Genres.NameOf(g.Key) ?? "Unknown", g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.GenreId)
            .ToList();

        var keywords = movies
            .SelectMany(m => m.Keywords)
            .GroupBy(k => k.Id)
            .Select(g => new KeywordCount(g.Key, g.First().Name, g.Count()))
            .OrderByDescending(k => k.Count)
            .ThenBy(k => k.Id)
            .ToList();

        var actors = movies
            .SelectMany(m => m.Cast)
            .GroupBy(c => c.Id)
            .Select(g => new ActorCount(g.Key, g.First().Name, g.First().ProfilePath, g.Count()))
            .OrderByDescending(a => a.Count)
            .ThenBy(a => a.Id)
            .ToList();

        var crew = movies
            .SelectMany(m => m.Crew)
            .Where(IsDirectorOrWriter)
            .GroupBy(c => c.Id)
            .Select(g => new CrewCount(g.Key, g.First().Name, g.First().ProfilePath, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Id)
            .ToList();

        var years = movies
            .Where(m => m.Movie.ReleaseDate is not null)
            .Select(m => m.Movie.ReleaseDate!.Value.Year)
            .ToList();

        var runtimes = movies
            .Where(m => m.Movie.Runtime is not null)
            .Select(m => (double)m.Movie.Runtime!.Value)
            .ToList();

        var voteAverages = movies
            .Where(m => m.Movie.VoteAverage is not null)
            .Select(m => m.Movie.VoteAverage!.Value)
            .ToList();

        return new CollectionInsightsResult(
            TotalMovies: movies.Count,
            UniqueGenres: genres.Count,
            MoviesByGenre: genres,
            UniqueKeywords: keywords.Count,
            TopKeywords: keywords.Take(TopKeywordLimit).ToList(),
            UniqueActors: actors.Count,
            TopActors: actors.Take(TopPeopleLimit).ToList(),
            UniqueCrew: crew.Count,
            TopCrew: crew.Take(TopPeopleLimit).ToList(),
            YearRange: years.Count > 0 ? (years.Min(), years.Max()) : null,
            AverageRuntime: runtimes.Count > 0 ? runtimes.Average() : null,
            AverageVoteAverage: voteAverages.Count > 0 ? voteAverages.Average() : null);
    }

    private static bool IsDirectorOrWriter(
        (int Id, string Name, string? ProfilePath, string Job, string Department) crew) =>
        CrewJobs.Contains(crew.Job) || CrewDepartments.Contains(crew.Department);
}

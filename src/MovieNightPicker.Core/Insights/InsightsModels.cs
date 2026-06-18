using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core.Insights;

/// <summary>
/// A collection movie enriched with the credit/keyword data a bare
/// <see cref="Movie"/> doesn't carry. The API layer builds these from TMDB
/// credits + keywords; <see cref="CollectionInsights"/> stays a pure function
/// over what it's given (the same split as the suggest flow's
/// <c>PreferenceExtractor.SelectedMovie</c>).
/// </summary>
public sealed record InsightsMovie(
    Movie Movie,
    IReadOnlyList<(int Id, string Name)> Keywords,
    IReadOnlyList<(int Id, string Name, string? ProfilePath)> Cast,
    IReadOnlyList<(int Id, string Name, string? ProfilePath, string Job, string Department)> Crew);

/// <summary>A genre and how many movies in the collection carry it.</summary>
public sealed record GenreCount(int GenreId, string GenreName, int Count);

/// <summary>A keyword and how many movies it tags.</summary>
public sealed record KeywordCount(int Id, string Name, int Count);

/// <summary>An actor and how many movies they appear in.</summary>
public sealed record ActorCount(int Id, string Name, string? ProfilePath, int Count);

/// <summary>A director/writer and how many of their credits the collection holds.</summary>
public sealed record CrewCount(int Id, string Name, string? ProfilePath, int Count);

/// <summary>
/// Aggregated statistics over a collection of enriched movies. Lists are ordered
/// most-frequent first; ranges/averages are null when no movie supplies the data.
/// </summary>
public sealed record CollectionInsightsResult(
    int TotalMovies,
    int UniqueGenres,
    IReadOnlyList<GenreCount> MoviesByGenre,
    int UniqueKeywords,
    IReadOnlyList<KeywordCount> TopKeywords,
    int UniqueActors,
    IReadOnlyList<ActorCount> TopActors,
    int UniqueCrew,
    IReadOnlyList<CrewCount> TopCrew,
    (int Min, int Max)? YearRange,
    double? AverageRuntime,
    double? AverageVoteAverage);

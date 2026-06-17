namespace MovieNightPicker.Core.Models;

/// <summary>
/// The preference profile distilled from a set of movies the user picked
/// during the suggest flow. Feeds the recommendation cascade.
/// </summary>
public sealed record MoviePreferences
{
    public IReadOnlyList<int> Genres { get; init; } = [];
    public IReadOnlyList<int> KeywordIds { get; init; } = [];
    public IReadOnlyList<int> Actors { get; init; } = [];
    public IReadOnlyList<int> Crew { get; init; } = [];
    public (int Start, int End)? YearRange { get; init; }
}

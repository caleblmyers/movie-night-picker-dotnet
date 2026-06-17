namespace MovieNightPicker.Core.Models;

/// <summary>
/// The user-facing shuffle inputs (15+ filters). These are the raw choices a
/// user makes; <see cref="DiscoverParams"/> is the resolved, TMDB-shaped query
/// produced from them by the discovery engine.
/// </summary>
public sealed record DiscoverFilters
{
    public IReadOnlyList<int> Genres { get; init; } = [];
    public (int Start, int End)? YearRange { get; init; }
    public string? Era { get; init; }
    public IReadOnlyList<int> Cast { get; init; } = [];
    public IReadOnlyList<int> Crew { get; init; } = [];
    public IReadOnlyList<int> Keywords { get; init; } = [];
    public string? Mood { get; init; }
    public (int Start, int End)? RuntimeRange { get; init; }
    public string? WatchProviders { get; init; }
    public IReadOnlyList<int> ExcludeGenres { get; init; } = [];
    public IReadOnlyList<int> ExcludeCast { get; init; } = [];
    public IReadOnlyList<int> ExcludeCrew { get; init; } = [];
    public (int Start, int End)? PopularityRange { get; init; }
    public string? PopularityLevel { get; init; }
    public IReadOnlyList<string> OriginCountries { get; init; } = [];
}

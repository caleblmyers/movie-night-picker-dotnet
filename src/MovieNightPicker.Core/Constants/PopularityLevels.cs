namespace MovieNightPicker.Core.Constants;

/// <summary>
/// Named TMDB popularity bands. A discover query resolves a level to a
/// <c>popularity.gte</c>/<c>popularity.lte</c> range.
/// </summary>
public static class PopularityLevels
{
    public static readonly (int Start, int End) High = (100, int.MaxValue);
    public static readonly (int Start, int End) Average = (20, 100);
    public static readonly (int Start, int End) Low = (0, 20);

    public static readonly IReadOnlyDictionary<string, (int Start, int End)> ByLevel =
        new Dictionary<string, (int Start, int End)>(StringComparer.OrdinalIgnoreCase)
        {
            ["HIGH"] = High,
            ["AVERAGE"] = Average,
            ["LOW"] = Low,
        };

    /// <summary>The popularity range for a level name, or null if unknown.</summary>
    public static (int Start, int End)? RangeFor(string level) =>
        ByLevel.TryGetValue(level, out var range) ? range : null;
}

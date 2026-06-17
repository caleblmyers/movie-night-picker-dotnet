namespace MovieNightPicker.Core.Constants;

/// <summary>
/// Maps an "era" string (a friendly decade/period label) to the inclusive
/// release-year range it covers. Open-ended eras ("modern", "2020-present")
/// extend to the current year.
/// </summary>
public static class EraYearRanges
{
    /// <summary>The current year, evaluated once per process start.</summary>
    public static int CurrentYear { get; } = DateTime.UtcNow.Year;

    public static readonly IReadOnlyDictionary<string, (int Start, int End)> ByEra =
        new Dictionary<string, (int Start, int End)>(StringComparer.OrdinalIgnoreCase)
        {
            ["classic"] = (1940, 1970),
            ["modern-era"] = (2010, CurrentYear),
            ["80s"] = (1980, 1989),
            ["90s"] = (1990, 1999),
            ["2000-2009"] = (2000, 2009),
            ["2010-2019"] = (2010, 2019),
            ["2020-present"] = (2020, CurrentYear),
        };

    /// <summary>The year range for an era, or null if the era is unknown.</summary>
    public static (int Start, int End)? RangeFor(string era) =>
        ByEra.TryGetValue(era, out var range) ? range : null;
}

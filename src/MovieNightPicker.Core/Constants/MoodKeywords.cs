namespace MovieNightPicker.Core.Constants;

/// <summary>
/// Maps a "mood" string the user can pick during shuffle/suggest to the TMDB
/// keyword ids that express it. A discover query merges these into its
/// <c>with_keywords</c> list.
/// </summary>
/// <remarks>
/// TODO: the original TS app ships ~37 moods. This is a faithful subset of the
/// key ones; port the remainder as the suggest UI is built out.
/// </remarks>
public static class MoodKeywords
{
    public static readonly IReadOnlyDictionary<string, int[]> ByMood =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["dark"] = [9715, 207317],
            ["fantasy"] = [156024],
            ["feel-good"] = [5615, 9799],
            ["romantic"] = [9748, 10235],
            ["suspenseful"] = [10714, 233151],
            ["funny"] = [9675, 18712],
            ["inspiring"] = [165194, 4344],
            ["scary"] = [10292, 11014],
            ["epic"] = [4565, 173177],
            ["nostalgic"] = [6027, 198482],
        };

    /// <summary>Keyword ids for a mood, or an empty array if the mood is unknown.</summary>
    public static int[] KeywordsFor(string mood) =>
        ByMood.TryGetValue(mood, out var ids) ? ids : [];
}

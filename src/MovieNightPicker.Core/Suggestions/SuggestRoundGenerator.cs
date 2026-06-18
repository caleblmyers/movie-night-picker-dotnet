using MovieNightPicker.Core.Constants;

namespace MovieNightPicker.Core.Suggestions;

/// <summary>
/// Produces the <see cref="CategoryRoundDef"/> for each of the ten suggest
/// rounds. The cycle probes genre, then era/mood/popularity anchored on a fixed
/// genre, repeats with a second anchor, and closes with an adaptive mixed round.
/// Pure — all data comes from the Wave-1 constant maps.
/// </summary>
public static class SuggestRoundGenerator
{
    private const string QualitySort = "vote_average.desc";
    private const string PopularitySort = "popularity.desc";

    private const int Action = 28;
    private const int Drama = 18;

    /// <summary>The genres spread across the four slots of each genre round.</summary>
    private static readonly int[] Round1Genres = [28, 18, 35, 878];    // Action, Drama, Comedy, Sci-Fi
    private static readonly int[] Round5Genres = [53, 27, 10749, 16];  // Thriller, Horror, Romance, Animation
    private static readonly int[] Round9Genres = [80, 12, 14, 36];     // Crime, Adventure, Fantasy, History

    /// <summary>Four distinct eras (from <see cref="EraYearRanges"/>) used by era rounds.</summary>
    private static readonly string[] EraSlots = ["classic", "90s", "2000-2009", "modern-era"];

    /// <summary>Four moods (from <see cref="MoodKeywords"/>) used by each anchor's mood round.</summary>
    private static readonly string[] DramaMoods = ["inspiring", "nostalgic", "dark", "romantic"];
    private static readonly string[] ActionMoods = ["suspenseful", "epic", "dark", "funny"];

    /// <summary>
    /// Build the round definition for <paramref name="round"/> (1-10).
    /// <paramref name="anchorGenre"/> only matters for round 10 (Mixed), which
    /// adapts to the user's most-picked genre; it defaults to Drama when unknown.
    /// </summary>
    public static CategoryRoundDef Generate(int round, int? anchorGenre = null) =>
        round switch
        {
            1 => GenreRound(Round1Genres),
            2 => EraRound(Drama),
            3 => MoodRound(Drama, DramaMoods),
            4 => PopularityRound(Drama),
            5 => GenreRound(Round5Genres),
            6 => EraRound(Action),
            7 => MoodRound(Action, ActionMoods),
            8 => PopularityRound(Action),
            9 => GenreRound(Round9Genres),
            10 => MixedRound(anchorGenre),
            _ => throw new ArgumentOutOfRangeException(
                nameof(round), round, "Round must be between 1 and 10."),
        };

    /// <summary>Four slots, one per genre, gated at the strict genre floor.</summary>
    private static CategoryRoundDef GenreRound(IReadOnlyList<int> genres)
    {
        var floor = QualityFloors.GenreRound;
        var slots = genres
            .Select(g => new SlotDefinition { Genres = [g] })
            .ToList();
        return new CategoryRoundDef(
            RoundCategory.Genre, "Genre", AnchorGenre: null,
            floor.VoteAverageGte, floor.VoteCountGte, QualitySort, slots);
    }

    /// <summary>Anchor genre crossed with four eras' year ranges.</summary>
    private static CategoryRoundDef EraRound(int anchorGenre)
    {
        var floor = QualityFloors.EraMoodRound;
        var slots = EraSlots
            .Select(era => new SlotDefinition
            {
                Genres = [anchorGenre],
                YearRange = EraYearRanges.RangeFor(era),
            })
            .ToList();
        return new CategoryRoundDef(
            RoundCategory.Era, "Era", anchorGenre,
            floor.VoteAverageGte, floor.VoteCountGte, QualitySort, slots);
    }

    /// <summary>Anchor genre crossed with four moods' keyword sets.</summary>
    private static CategoryRoundDef MoodRound(int anchorGenre, IReadOnlyList<string> moods)
    {
        var floor = QualityFloors.EraMoodRound;
        var slots = moods
            .Select(mood => new SlotDefinition
            {
                Genres = [anchorGenre],
                KeywordIds = MoodKeywords.KeywordsFor(mood),
            })
            .ToList();
        return new CategoryRoundDef(
            RoundCategory.Mood, "Mood", anchorGenre,
            floor.VoteAverageGte, floor.VoteCountGte, QualitySort, slots);
    }

    /// <summary>
    /// Anchor genre across the three named popularity bands plus an "any
    /// popularity" catch-all, ranked by popularity rather than rating.
    /// </summary>
    private static CategoryRoundDef PopularityRound(int anchorGenre)
    {
        var floor = QualityFloors.PopularityRound;
        var slots = new List<SlotDefinition>
        {
            new() { Genres = [anchorGenre], PopularityRange = PopularityLevels.High },
            new() { Genres = [anchorGenre], PopularityRange = PopularityLevels.Average },
            new() { Genres = [anchorGenre], PopularityRange = PopularityLevels.Low },
            new() { Genres = [anchorGenre] },
        };
        return new CategoryRoundDef(
            RoundCategory.Popularity, "Popularity", anchorGenre,
            floor.VoteAverageGte, floor.VoteCountGte, PopularitySort, slots);
    }

    /// <summary>
    /// The closing round: one slot each for genre, era, mood, and popularity, all
    /// anchored on the user's most-picked genre (Drama when there's no signal).
    /// </summary>
    private static CategoryRoundDef MixedRound(int? anchorGenre)
    {
        var floor = QualityFloors.EraMoodRound;
        var anchor = anchorGenre ?? Drama;
        var slots = new List<SlotDefinition>
        {
            new() { Genres = [anchor] },
            new() { Genres = [anchor], YearRange = EraYearRanges.RangeFor("modern-era") },
            new() { Genres = [anchor], KeywordIds = MoodKeywords.KeywordsFor("dark") },
            new() { Genres = [anchor], PopularityRange = PopularityLevels.Average },
        };
        return new CategoryRoundDef(
            RoundCategory.Mixed, "Mixed", anchor,
            floor.VoteAverageGte, floor.VoteCountGte, QualitySort, slots);
    }
}

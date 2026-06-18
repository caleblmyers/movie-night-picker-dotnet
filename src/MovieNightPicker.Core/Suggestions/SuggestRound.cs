using MovieNightPicker.Core.Models;

namespace MovieNightPicker.Core.Suggestions;

/// <summary>
/// The flavour of a suggest round. The 10-round flow cycles through these to
/// probe a user's taste from several angles before the cascade recommends.
/// </summary>
public enum RoundCategory
{
    Genre,
    Era,
    Mood,
    Popularity,
    Mixed,
}

/// <summary>
/// One of the four choices presented in a round. A slot is a partial discover
/// query — genre(s) plus at most one distinguishing axis (an era's year range,
/// a mood's keywords, or a popularity band). Null vote floors mean "inherit the
/// round's defaults".
/// </summary>
public sealed record SlotDefinition
{
    public IReadOnlyList<int> Genres { get; init; } = [];
    public (int Start, int End)? YearRange { get; init; }
    public IReadOnlyList<int> KeywordIds { get; init; } = [];
    public (int Start, int End)? PopularityRange { get; init; }
    public double? VoteAverageGte { get; init; }
    public int? VoteCountGte { get; init; }
}

/// <summary>
/// The recipe for a single round: its category, a display label, the default
/// quality floor / sort applied to every slot that doesn't override them, the
/// genre the round anchors on (null for genre rounds, which spread across four
/// distinct genres), and exactly four <see cref="SlotDefinition"/>s.
/// </summary>
public sealed record CategoryRoundDef(
    RoundCategory Category,
    string CategoryLabel,
    int? AnchorGenre,
    double DefaultVoteAverageGte,
    int DefaultVoteCountGte,
    string DefaultSortBy,
    IReadOnlyList<SlotDefinition> Slots);

/// <summary>The four movies served for a round, plus the round's category/label.</summary>
public sealed record SuggestRoundResult(
    IReadOnlyList<Movie> Movies,
    RoundCategory Category,
    string CategoryLabel);

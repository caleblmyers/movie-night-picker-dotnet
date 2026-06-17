namespace MovieNightPicker.Core.Constants;

/// <summary>
/// A minimum-quality gate applied to a discover query: a vote-average floor and
/// a vote-count floor (so a 9.0 from three voters doesn't slip through).
/// </summary>
public readonly record struct QualityFloor(double VoteAverageGte, int VoteCountGte);

/// <summary>
/// The named quality floors used across the discovery and suggestion rounds,
/// from strictest to most relaxed.
/// </summary>
public static class QualityFloors
{
    public static readonly QualityFloor GenreRound = new(6.5, 300);
    public static readonly QualityFloor EraMoodRound = new(6.5, 150);
    public static readonly QualityFloor PopularityRound = new(6.0, 50);
    public static readonly QualityFloor CascadeDefault = new(6.5, 150);
    public static readonly QualityFloor Emergency = new(6.0, 100);
    public static readonly QualityFloor Desperate = new(5.0, 50);
}

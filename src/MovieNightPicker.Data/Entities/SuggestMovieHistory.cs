namespace MovieNightPicker.Data.Entities;

/// <summary>
/// Tracks movies already surfaced by the suggestion engine for a user, so the
/// recommendation cascade can exclude them. Unique per (user, movie).
/// </summary>
public class SuggestMovieHistory
{
    public int Id { get; set; }

    public int TmdbId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}

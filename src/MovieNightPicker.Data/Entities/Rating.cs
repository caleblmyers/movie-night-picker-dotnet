namespace MovieNightPicker.Data.Entities;

/// <summary>
/// A user's rating (1-10) for a movie. Unique per (user, movie).
/// </summary>
public class Rating
{
    public int Id { get; set; }

    public int TmdbId { get; set; }

    /// <summary>Rating value on a 1-10 scale.</summary>
    public int RatingValue { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}

namespace MovieNightPicker.Data.Entities;

/// <summary>
/// A user's written review for a movie. Unique per (user, movie).
/// </summary>
public class Review
{
    public int Id { get; set; }

    public int TmdbId { get; set; }

    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}

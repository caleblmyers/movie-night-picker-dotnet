namespace MovieNightPicker.Data.Entities;

/// <summary>
/// A movie the user has saved/bookmarked. Stores only the TMDB id; movie
/// details are fetched live from TMDB. Unique per (user, movie).
/// </summary>
public class SavedMovie
{
    public int Id { get; set; }

    public int TmdbId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}

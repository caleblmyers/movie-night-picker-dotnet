namespace MovieNightPicker.Data.Entities;

/// <summary>
/// A record of a movie the user has marked as watched.
/// </summary>
public class MovieHistory
{
    public int Id { get; set; }

    /// <summary>TMDB movie id.</summary>
    public int MovieId { get; set; }

    public required string Title { get; set; }

    public string? PosterUrl { get; set; }

    public DateTime WatchedAt { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}
